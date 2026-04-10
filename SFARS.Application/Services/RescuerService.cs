using MapsterMapper;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Rescuer;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;

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
}