using MapsterMapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Facility;
using SFARS.Application.Validations;
using SFARS.Application.Exceptions;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.Params;
using SFARS.Infrastructure.Helpers;

namespace SFARS.Application.Services;

/// <summary>
/// Medical facility service: production-grade nearby search + admin CRUD.
/// Cache strategy: IMemoryCache with TTL for active facility list.
/// Bust cache on every write operation.
/// </summary>
public class MedicalFacilityService : GenericService<MedicalFacility, FacilityDto, Guid>, IMedicalFacilityService<FacilityDto>
{
    private readonly IMemoryCache _memoryCache;

    private const string FacilitiesCacheKey = LocationConstants.MemCacheFacilitiesActiveKey;
    private static readonly TimeSpan FacilitiesCacheTtl = LocationConstants.FacilitiesCacheTtl;

    private const int StaleAntivenomDays = 30;

    public MedicalFacilityService(
        ISystemMessageService msgService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IMemoryCache memoryCache,
        ILogger<MedicalFacilityService> logger) : base(msgService, unitOfWork, mapper, logger)
    {
        _memoryCache = memoryCache;
    }

    #region User-facing: Nearby Search

    /// <summary>
    /// Find active medical facilities within a given radius, sorted by distance.
    /// Uses bounding box pre-filter + IMemoryCache for production efficiency.
    /// Requires lat/lng parameters to be provided.
    /// </summary>
    public async Task<IServiceResult> GetNearbyFacilitiesAsync(
        Guid userId, double? latitude, double? longitude, double radiusKm = 10, int limit = 20)
    {
        // Add fallback to user's location if parameters are missing
        if (!latitude.HasValue || !longitude.HasValue)
        {
            var user = await _unitOfWork.Repository<User, Guid>().GetByIdAsync(userId);
            if (user?.CurrentLocation != null)
            {
                latitude = user.CurrentLocation.Y;
                longitude = user.CurrentLocation.X;
            }
        }

        // Validate required parameters
        if (!latitude.HasValue || !longitude.HasValue)
        {
            return new ServiceResult(
                ResultCodeConst.Medical_Warning0006,
                await _msgService.GetMessageAsync(ResultCodeConst.Medical_Warning0006),
                null);
        }

        var facilities = await GetCachedFacilitiesAsync();

        var radiusMeters = radiusKm * 1000;
        var lat = latitude.Value;
        var lng = longitude.Value;
        var (minLat, maxLat, minLng, maxLng) = LocationHelper.BoundingBox(lat, lng, radiusKm);

        var nearbyFacilities = facilities
            .Where(f => f.Location.Y >= minLat && f.Location.Y <= maxLat
                     && f.Location.X >= minLng && f.Location.X <= maxLng)
            .Select(f => new
            {
                Facility = f,
                Distance = LocationHelper.HaversineMeters(
                    lat, lng, f.Location.Y, f.Location.X)
            })
            .Where(x => x.Distance <= radiusMeters)
            .OrderBy(x => x.Distance)
            .Take(limit)
            .Select(x =>
            {
                var dto = MapToDto(x.Facility);
                dto.DistanceMeters = x.Distance;
                return dto;
            })
            .ToList();

        if (!nearbyFacilities.Any())
        {
            return new ServiceResult(
                ResultCodeConst.Medical_Warning0001,
                await _msgService.GetMessageAsync(ResultCodeConst.Medical_Warning0001),
                nearbyFacilities);
        }

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            nearbyFacilities);
    }

    #endregion

    #region Admin: CRUD Operations

    /// <summary>
    /// [Admin] Get paginated facility list with filters.
    /// </summary>
    public async Task<IServiceResult> GetAllFacilitiesAsync(FacilitySpecParams specParams)
    {
        var repo = _unitOfWork.Repository<MedicalFacility, Guid>();

        // Get paginated data
        var spec = MedicalFacilitySpecification.WithFilters(specParams);
        var facilities = await repo.GetAllWithSpecAsync(spec, tracked: false);

        // Get total count for pagination
        var countSpec = MedicalFacilitySpecification.CountSpec(specParams);
        var totalCount = await repo.CountAsync(countSpec);

        var dtos = facilities.Select(MapToDto).ToList();

        var pagedResult = new
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = specParams.GetPage(),
            Limit = specParams.Limit ?? dtos.Count
        };

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            pagedResult);
    }

    /// <summary>
    /// [Admin] Get facility by ID.
    /// </summary>
    public async Task<IServiceResult> GetFacilityByIdAsync(Guid id)
    {
        var facility = await _unitOfWork.Repository<MedicalFacility, Guid>().GetByIdAsync(id);
        if (facility == null)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));
        }

        return new ServiceResult(
            ResultCodeConst.Medical_Success0001,
            await _msgService.GetMessageAsync(ResultCodeConst.Medical_Success0001),
            MapToDto(facility));
    }

    /// <summary>
    /// [Admin] Create a new medical facility.
    /// </summary>
    public async Task<IServiceResult> CreateFacilityAsync(FacilityDto dto)
    {
        // Validate
        var validationResult = await ValidatorExtensions.ValidateAsync(dto);
        if (validationResult != null && !validationResult.IsValid)
        {
            var errors = validationResult.ToProblemDetails().Errors;
            throw new UnprocessableEntityException("Validation errors", errors);
        }

        var entity = _mapper.Map<MedicalFacility>(dto);

        entity.Location = new Point(dto.Longitude, dto.Latitude) { SRID = 4326 };
        entity.AntivenomUpdatedAt = dto.HasAntivenom ? DateTime.UtcNow : null;
        entity.IsActive = true;

        await _unitOfWork.Repository<MedicalFacility, Guid>().AddAsync(entity);

        if (await _unitOfWork.SaveChangesAsync() > 0)
        {
            BustCache();
            _logger.LogInformation("Created medical facility {FacilityId} '{FacilityName}'",
                entity.Id, entity.Name);

            return new ServiceResult(
                ResultCodeConst.Medical_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.Medical_Success0002),
                MapToDto(entity));
        }

        return new ServiceResult(
            ResultCodeConst.SYS_Fail0001,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
    }

    /// <summary>
    /// [Admin] Update an existing medical facility.
    /// </summary>
    public async Task<IServiceResult> UpdateFacilityAsync(Guid id, FacilityDto dto)
    {
        // Validate
        var validationResult = await ValidatorExtensions.ValidateAsync(dto);
        if (validationResult != null && !validationResult.IsValid)
        {
            var errors = validationResult.ToProblemDetails().Errors;
            throw new UnprocessableEntityException("Validation errors", errors);
        }

        var facility = await _unitOfWork.Repository<MedicalFacility, Guid>().GetByIdAsync(id);
        if (facility == null)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));
        }

        // Track antivenom change
        var antivenomChanged = facility.HasAntivenom != dto.HasAntivenom;

        // Update fields
        facility.Name = dto.Name;
        facility.Type = dto.FacilityType;
        facility.Address = dto.Address;
        facility.Province = dto.Province;
        facility.Location = new Point(dto.Longitude, dto.Latitude) { SRID = 4326 };
        facility.PhoneNumber = dto.PhoneNumber;
        facility.Email = dto.Email;
        facility.Website = dto.Website;
        facility.OpenHours = dto.OpenHours;
        facility.CloseHours = dto.CloseHours;
        facility.EmergencyAvailable = dto.EmergencyAvailable;
        facility.HasAntivenom = dto.HasAntivenom;
        facility.Notes = dto.Notes;
        facility.UpdatedAt = DateTime.UtcNow;

        if (antivenomChanged)
        {
            facility.AntivenomUpdatedAt = DateTime.UtcNow;
        }

        await _unitOfWork.Repository<MedicalFacility, Guid>().UpdateAsync(facility);

        if (await _unitOfWork.SaveChangesAsync() > 0)
        {
            BustCache();
            _logger.LogInformation("Updated medical facility {FacilityId} '{FacilityName}'",
                facility.Id, facility.Name);

            return new ServiceResult(
                ResultCodeConst.Medical_Success0003,
                await _msgService.GetMessageAsync(ResultCodeConst.Medical_Success0003),
                MapToDto(facility));
        }

        return new ServiceResult(
            ResultCodeConst.SYS_Fail0003,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0003));
    }

    /// <summary>
    /// [Admin] Update antivenom availability (dedicated endpoint for quick updates).
    /// </summary>
    public async Task<IServiceResult> UpdateAntivenomAsync(Guid id, bool hasAntivenom)
    {
        var facility = await _unitOfWork.Repository<MedicalFacility, Guid>().GetByIdAsync(id);
        if (facility == null)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));
        }

        facility.HasAntivenom = hasAntivenom;
        facility.AntivenomUpdatedAt = DateTime.UtcNow;
        facility.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<MedicalFacility, Guid>().UpdateAsync(facility);

        if (await _unitOfWork.SaveChangesAsync() > 0)
        {
            BustCache();
            _logger.LogInformation(
                "Updated antivenom status for facility {FacilityId} to {HasAntivenom}",
                facility.Id, hasAntivenom);

            return new ServiceResult(
                ResultCodeConst.Medical_Success0004,
                await _msgService.GetMessageAsync(ResultCodeConst.Medical_Success0004),
                MapToDto(facility));
        }

        return new ServiceResult(
            ResultCodeConst.SYS_Fail0003,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0003));
    }

    /// <summary>
    /// [Admin] Deactivate a facility (soft delete — IsActive = false).
    /// </summary>
    public async Task<IServiceResult> DeactivateFacilityAsync(Guid id)
    {
        var facility = await _unitOfWork.Repository<MedicalFacility, Guid>().GetByIdAsync(id);
        if (facility == null)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));
        }

        if (!facility.IsActive)
        {
            return new ServiceResult(
                ResultCodeConst.Medical_Warning0003,
                await _msgService.GetMessageAsync(ResultCodeConst.Medical_Warning0003));
        }

        facility.IsActive = false;
        facility.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<MedicalFacility, Guid>().UpdateAsync(facility);

        if (await _unitOfWork.SaveChangesAsync() > 0)
        {
            BustCache();
            _logger.LogInformation("Deactivated medical facility {FacilityId} '{FacilityName}'",
                facility.Id, facility.Name);

            return new ServiceResult(
                ResultCodeConst.Medical_Success0005,
                await _msgService.GetMessageAsync(ResultCodeConst.Medical_Success0005),
                MapToDto(facility));
        }

        return new ServiceResult(
            ResultCodeConst.SYS_Fail0003,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0003));
    }

    /// <summary>
    /// [Admin] Reactivate a deactivated facility.
    /// </summary>
    public async Task<IServiceResult> ActivateFacilityAsync(Guid id)
    {
        var facility = await _unitOfWork.Repository<MedicalFacility, Guid>().GetByIdAsync(id);
        if (facility == null)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));
        }

        if (facility.IsActive)
        {
            return new ServiceResult(
                ResultCodeConst.Medical_Warning0004,
                await _msgService.GetMessageAsync(ResultCodeConst.Medical_Warning0004));
        }

        facility.IsActive = true;
        facility.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<MedicalFacility, Guid>().UpdateAsync(facility);

        if (await _unitOfWork.SaveChangesAsync() > 0)
        {
            BustCache();
            _logger.LogInformation("Activated medical facility {FacilityId} '{FacilityName}'",
                facility.Id, facility.Name);

            return new ServiceResult(
                ResultCodeConst.Medical_Success0006,
                await _msgService.GetMessageAsync(ResultCodeConst.Medical_Success0006),
                MapToDto(facility));
        }

        return new ServiceResult(
            ResultCodeConst.SYS_Fail0003,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0003));
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Get all active facilities from IMemoryCache.
    /// Single DB query per TTL across all requests.
    /// </summary>
    private async Task<IEnumerable<MedicalFacility>> GetCachedFacilitiesAsync()
    {
        if (_memoryCache.TryGetValue(FacilitiesCacheKey, out IEnumerable<MedicalFacility>? cached)
            && cached != null)
        {
            return cached;
        }

        var facilities = await _unitOfWork.Repository<MedicalFacility, Guid>()
            .GetAllWithSpecAsync(new BaseSpecification<MedicalFacility>(f => f.IsActive),
                tracked: false);

        _memoryCache.Set(FacilitiesCacheKey, facilities, FacilitiesCacheTtl);

        _logger.LogDebug("Loaded {Count} active facilities into memory cache", facilities.Count());
        return facilities;
    }

    /// <summary>
    /// Bust the in-memory facility cache after any write operation.
    /// </summary>
    private void BustCache()
    {
        _memoryCache.Remove(FacilitiesCacheKey);
    }

    /// <summary>
    /// Map MedicalFacility entity to FacilityDto.
    /// Flattens NTS Point to lat/lon, computes IsAntivenomStale.
    /// </summary>
    private static FacilityDto MapToDto(MedicalFacility f)
    {
        return new FacilityDto
        {
            Id = f.Id,
            Name = f.Name,
            FacilityType = f.Type,
            Latitude = f.Location.Y,
            Longitude = f.Location.X,
            Address = f.Address,
            Province = f.Province,
            PhoneNumber = f.PhoneNumber,
            Email = f.Email,
            Website = f.Website,
            OpenHours = f.OpenHours,
            CloseHours = f.CloseHours,
            EmergencyAvailable = f.EmergencyAvailable,
            HasAntivenom = f.HasAntivenom,
            AntivenomUpdatedAt = f.AntivenomUpdatedAt,
            IsActive = f.IsActive,
            IsAntivenomStale = f.HasAntivenom
                && f.AntivenomUpdatedAt.HasValue
                && (DateTime.UtcNow - f.AntivenomUpdatedAt.Value).TotalDays > StaleAntivenomDays,
            Notes = f.Notes,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        };
    }

    #endregion
}