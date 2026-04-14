using MapsterMapper;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Rescuer;
using SFARS.Application.Dtos.User;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Specifications.Params;
using SFARS.Domain.Specifications.Users;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Common.Constants;
using SFARS.Application.Dtos;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Services;

public class RescuerService : IRescuerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<RescuerService> _logger;
    private readonly ISystemMessageService _msgService;

    public RescuerService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<RescuerService> logger,
        ISystemMessageService msgService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _msgService = msgService;
    }

    /// <summary>
    /// Get the authenticated rescuer's combined profile:
    /// User info (firstName, lastName, phone, avatar, address, gender, dob)
    /// and RescuerProfile fields (experienceYears, vehicleType, licensePlate, coverageRadiusKM, isAvailable).
    /// </summary>
    public async Task<IServiceResult> GetRescuerProfileAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return new ServiceResult(
                ResultCodeConst.Auth_Warning0007,
                await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0007)
            );
        }

        // Fetch User entity
        var user = await _unitOfWork.Repository<User, Guid>().GetByIdAsync(userId);
        if (user == null)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0004,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004)
            );
        }

        // Fetch RescuerProfile entity
        var profile = await _unitOfWork.Repository<RescuerProfile, Guid>().GetByIdAsync(userId);
        if (profile == null)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0004,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004)
            );
        }

        // Build combined response DTO
        var responseDto = new RescuerProfileDto
        {
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            Avatar = user.Avatar,
            Address = user.Address,
            Gender = user.Gender,
            Dob = user.Dob,
            ExperienceYears = profile.ExperienceYears,
            VehicleType = profile.VehicleType,
            LicensePlate = profile.LicensePlate,
            CoverageRadiusKM = profile.CoverageRadiusKM,
            IsAvailable = profile.IsAvailable,
            AvailableUpdatedAt = profile.AvailableUpdatedAt
        };

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            responseDto
        );
    }

    /// <summary>
    /// Update the authenticated rescuer's combined profile:
    /// both User info (firstName, lastName, phone, avatar, address, gender, dob)
    /// and RescuerProfile fields (experienceYears, vehicleType, licensePlate, coverageRadiusKM, isAvailable).
    /// </summary>
    public async Task<IServiceResult> UpdateRescuerProfileAsync<TDto>(Guid userId, TDto dto) where TDto : class
    {
        if (userId == Guid.Empty)
        {
            return new ServiceResult(
                ResultCodeConst.Auth_Warning0007,
                await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0007)
            );
        }

        if (dto is not RescuerProfileDto profileDto)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001)
            );
        }

        // Fetch User entity
        var user = await _unitOfWork.Repository<User, Guid>().GetByIdAsync(userId);
        if (user == null)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0004,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004)
            );
        }

        // Fetch RescuerProfile entity
        var profile = await _unitOfWork.Repository<RescuerProfile, Guid>().GetByIdAsync(userId);
        if (profile == null)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0004,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004)
            );
        }

        // Update User fields
        user.FirstName = profileDto.FirstName;
        user.LastName = profileDto.LastName;
        user.Phone = profileDto.Phone;
        user.Avatar = profileDto.Avatar;
        user.Address = profileDto.Address;
        user.Gender = profileDto.Gender;
        user.Dob = profileDto.Dob;
        user.UpdatedAt = DateTime.UtcNow;

        // Update RescuerProfile fields
        profile.ExperienceYears = profileDto.ExperienceYears;
        profile.VehicleType = profileDto.VehicleType;   // enum VehicleType
        profile.LicensePlate = profileDto.LicensePlate;
        profile.CoverageRadiusKM = profileDto.CoverageRadiusKM;
        profile.IsAvailable = profileDto.IsAvailable;
        profile.AvailableUpdatedAt = DateTime.UtcNow;

        // Persist both in one SaveChanges
        await _unitOfWork.Repository<User, Guid>().UpdateAsync(user);
        await _unitOfWork.Repository<RescuerProfile, Guid>().UpdateAsync(profile);
        await _unitOfWork.SaveChangesAsync();

        // Build combined response DTO
        var responseDto = new RescuerProfileDto
        {
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            Avatar = user.Avatar,
            Address = user.Address,
            Gender = user.Gender,
            Dob = user.Dob,
            ExperienceYears = profile.ExperienceYears,
            VehicleType = profile.VehicleType,
            LicensePlate = profile.LicensePlate,
            CoverageRadiusKM = profile.CoverageRadiusKM,
            IsAvailable = profile.IsAvailable,
            AvailableUpdatedAt = profile.AvailableUpdatedAt
        };

        return new ServiceResult(
            ResultCodeConst.SYS_Success0003,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0003),
            responseDto
        );
    }

    /// <summary>
    /// Get a paginated list of all verified rescuers with public metrics.
    /// High-performance projection using GetAllWithSpecAndSelectorAsync to avoid N+1 queries.
    /// </summary>
    public async Task<IServiceResult> GetAllRescuersPublicAsync(PublicRescuerSpecParams specParams)
    {
        specParams ??= new PublicRescuerSpecParams();

        var page = specParams.GetPage();
        var limit = specParams.GetTake();

        try
        {
            var countSpec = UserSpecification.PublicRescuersCount(specParams);
            var totalItems = await _unitOfWork.Repository<User, Guid>().CountAsync(countSpec);

            if (totalItems == 0)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004),
                    new PaginatedResultDto<PublicRescuerDto>(
                        Enumerable.Empty<PublicRescuerDto>(), page, limit, 0, totalItems));
            }

            var spec = UserSpecification.PublicRescuersList(specParams);

            // Fetch Mission Queryable to do lateral subquery mapping for TotalMissions
            var missionQuery = _unitOfWork.Repository<RescueMission, Guid>().GetQueryable(tracked: false);

            var dtos = await _unitOfWork.Repository<User, Guid>().GetAllWithSpecAndSelectorAsync(
                spec,
                u => new PublicRescuerDto
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Phone = u.Phone,
                    Avatar = u.Avatar,
                    Address = u.Address,
                    Gender = u.Gender,
                    Dob = u.Dob,
                    Status = u.Status,
                    IsOnline = u.IsOnline,
                    LastActiveAt = u.LastActiveAt,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt,
                    Latitude = u.CurrentLocation != null ? u.CurrentLocation.Y : default(double?),
                    Longitude = u.CurrentLocation != null ? u.CurrentLocation.X : default(double?),
                    // Subquery exactly mapped to SQL, zero N+1 issues
                    TotalMissions = missionQuery.Count(m => m.RescuerId == u.Id && m.Status == RescueStatus.Completed) 
                },
                tracked: false
            );

            var totalPages = (int)Math.Ceiling((double)totalItems / limit);

            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                new PaginatedResultDto<PublicRescuerDto>(dtos, page, limit, totalPages, totalItems)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving public rescuer list");
            throw;
        }
    }
}