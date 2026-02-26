using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Facility;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;
using SFARS.Infrastructure.Helpers;

namespace SFARS.Application.Services;

/// <summary>
/// Production-grade medical facility search:
/// 1. IMemoryCache for full facility list (5-min TTL, facilities rarely change)
/// 2. Bounding box SQL pre-filter (reduces N → ~50 rows)
/// 3. Haversine on reduced set + sort + take limit
/// </summary>
public class MedicalFacilityService : IMedicalFacilityService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemMessageService _msgService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<MedicalFacilityService> _logger;

    private const string FacilitiesCacheKey = LocationConstants.MemCacheFacilitiesActiveKey;
    private static readonly TimeSpan FacilitiesCacheTtl = LocationConstants.FacilitiesCacheTtl;

    public MedicalFacilityService(
        IUnitOfWork unitOfWork,
        ISystemMessageService msgService,
        IMemoryCache memoryCache,
        ILogger<MedicalFacilityService> logger)
    {
        _unitOfWork = unitOfWork;
        _msgService = msgService;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// Find active medical facilities within a given radius, sorted by distance.
    /// Uses bounding box pre-filter + IMemoryCache for production efficiency.
    /// </summary>
    public async Task<IServiceResult> GetNearbyFacilitiesAsync(
        double latitude, double longitude, double radiusKm = 10, int limit = 20)
    {
        // Get facilities from in-memory cache (5-min TTL, one DB query per 5 min)
        var facilities = await GetCachedFacilitiesAsync();

        var radiusMeters = radiusKm * 1000;

        // Bounding box pre-filter (eliminates ~95% of facilities before Haversine)
        var (minLat, maxLat, minLng, maxLng) = LocationHelper.BoundingBox(latitude, longitude, radiusKm);

        var nearbyFacilities = facilities
            // Fast rectangular pre-filter
            .Where(f => f.Location.Y >= minLat && f.Location.Y <= maxLat
                     && f.Location.X >= minLng && f.Location.X <= maxLng)
            // Precise circular distance on remaining candidates
            .Select(f => new NearbyFacilityDto
            {
                Id = f.Id,
                Name = f.Name,
                Type = f.Type.ToString(),
                Address = f.Address,
                PhoneNumber = f.PhoneNumber,
                OperatingHours = f.OperatingHours,
                Latitude = f.Location.Y,
                Longitude = f.Location.X,
                DistanceMeters = LocationHelper.HaversineMeters(
                    latitude, longitude, f.Location.Y, f.Location.X)
            })
            .Where(f => f.DistanceMeters <= radiusMeters)
            .OrderBy(f => f.DistanceMeters)
            .Take(limit)
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

    /// <summary>
    /// Get all active facilities from IMemoryCache.
    /// Single DB query per 5 minutes across all requests.
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
}