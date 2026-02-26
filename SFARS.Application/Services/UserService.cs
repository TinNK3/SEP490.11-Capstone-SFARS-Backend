using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using SFARS.Application.Common;
using SFARS.Application.Dtos.User;
using SFARS.Application.Events;
using SFARS.Application.Validations;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.Users;
using SFARS.Infrastructure.Helpers;

namespace SFARS.Application.Services
{
    public class UserService : GenericService<User, UserDto, Guid>, IUserService<UserDto>
    {
        private readonly IPublisher _publisher;

        public UserService(
            ISystemMessageService msgService,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<UserService> logger,
            IPublisher publisher) : base(msgService, unitOfWork, mapper, logger)
        {
            _publisher = publisher;
        }

        /// <summary>
        /// Get user by email address (for authentication)
        /// </summary>
        public async Task<IServiceResult> GetByEmailAsync(string email)
        {
            //Query user with roles included
            var baseSpec = new BaseSpecification<User>(u => u.Email.Equals(email));

            baseSpec.ApplyInclude(u => u.Include(ur => ur.UserRoles)
                                                .ThenInclude(r => r.Role));

            // Get user
            var user =  await _unitOfWork.Repository<User, Guid>().GetWithSpecAsync(baseSpec);

            //Not exits user
            if (user == null)
                return new ServiceResult(ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));

            // Response read success
            return new ServiceResult(ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                _mapper.Map<UserDto>(user));
        }

        /// <summary>
        /// Retrieves the profile of the currently authenticated user.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<IServiceResult> GetMeAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0007,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0007)
                );
            }

            var spec = new UserByIdWithRoleSpecification(userId);
            var user = await _unitOfWork.Repository<User, Guid>()
                                        .GetWithSpecAsync(spec);

            if (user == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004)
                );
            }

            var dto = _mapper.Map<UserDto>(user);

            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                dto
            );
        }

        /// <summary>
        /// Update the profile of the currently authenticated user.
        /// Only updates allowed fields: FirstName, LastName, Phone, Avatar, Address, Gender, Dob
        /// </summary>
        public async Task<IServiceResult> UpdateMeAsync(Guid userId, UserDto dto)
        {
            if (userId == Guid.Empty)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0007,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0007)
                );
            }

            var validation = await ValidatorExtensions.ValidateAsync(dto);
            if (validation != null && !validation.IsValid)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001),
                    validation.ToProblemDetails().Errors
                );
            }

            // Get existing user
            var user = await _unitOfWork.Repository<User, Guid>().GetByIdAsync(userId);

            if (user == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004)
                );
            }

            // Update only allowed fields
            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.Phone = dto.Phone;
            user.Address = dto.Address;
            user.Avatar = dto.Avatar;
            user.Gender = dto.Gender;
            user.Dob = dto.Dob;
            user.UpdatedAt = DateTime.UtcNow;

            // Save changes
            await _unitOfWork.Repository<User, Guid>().UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var spec = new UserByIdWithRoleSpecification(userId);
            var userWithRole = await _unitOfWork.Repository<User, Guid>()
                .GetWithSpecAsync(spec, tracked: false);

            if (userWithRole == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004)
                );
            }

            return new ServiceResult(
                ResultCodeConst.SYS_Success0003,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0003),
                _mapper.Map<UserDto>(userWithRole)
            );
        }

        public async Task<IServiceResult> UpdateEmailVerificationCodeAsync(Guid userId, string otp)
        {
            // Initiate service result
            var serviceResult = new ServiceResult();

            try
            {
                // Retrieve the entity
                var existingEntity = await _unitOfWork.Repository<User, Guid>().GetByIdAsync(userId);

                //Check not found
                if (existingEntity == null)
                {
                    return new ServiceResult(ResultCodeConst.SYS_Fail0002,
                        await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002), false);
                }

                // Update email verification code
                existingEntity.EmailVerificationCode = otp;

                // Check if there are any differences between the original and the updated entity
                if (!_unitOfWork.Repository<User, Guid>().HasChanges(existingEntity))
                {
                    serviceResult.ResultCode = ResultCodeConst.SYS_Success0003;
                    serviceResult.Message = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0003);
                    serviceResult.Data = true;
                    return serviceResult;
                }

                // Progress update when all require passed
                await _unitOfWork.Repository<User, Guid>().UpdateAsync(existingEntity);

                // Commit the changes
                var commitResult = await _unitOfWork.SaveChangesAsync();

                // Check commit result
                if (commitResult == 0)
                {
                    serviceResult.ResultCode = ResultCodeConst.SYS_Fail0003;
                    serviceResult.Message = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0003);
                    serviceResult.Data = false;
                    return serviceResult;
                }

                // Response update success
                serviceResult.ResultCode = ResultCodeConst.SYS_Success0003;
                serviceResult.Message = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0003);
                serviceResult.Data = true;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.Message);
                throw new Exception("Error invoke while confirm email verification code");
            }

            return serviceResult;
        }

        public async Task<IServiceResult> UpdatePasswordAsync(Guid userId, string newPasswordHash)
        {
            try
            {
                // Retrieve the entity
                var existingEntity = await _unitOfWork.Repository<User, Guid>().GetByIdAsync(userId);

                // Check not found
                if (existingEntity == null)
                {
                    return new ServiceResult(ResultCodeConst.SYS_Warning0004,
                        await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004), false);
                }

                // Update password hash and clear email verification code
                existingEntity.PasswordHash = newPasswordHash;
                existingEntity.EmailVerificationCode = null;
                existingEntity.UpdatedAt = DateTime.UtcNow;

                // Progress update
                await _unitOfWork.Repository<User, Guid>().UpdateAsync(existingEntity);

                // Commit the changes
                var commitResult = await _unitOfWork.SaveChangesAsync();

                // Check commit result
                if (commitResult == 0)
                {
                    return new ServiceResult(ResultCodeConst.SYS_Fail0003,
                        await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0003), false);
                }

                // Response update success
                return new ServiceResult(ResultCodeConst.SYS_Success0003,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0003), true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating password for user {UserId}", userId);
                throw new Exception("Error occurred while updating password");
            }
        }

        #region Location

        /// <summary>
        /// Get the current location of the authenticated user.
        /// </summary>
        public async Task<IServiceResult> GetUserLocationAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0007,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0007));
            }

            var user = await _unitOfWork.Repository<User, Guid>().GetByIdAsync(userId);
            if (user == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
            }

            var dto = new UserLocationDto
            {
                Latitude = user.CurrentLocation?.Y,
                Longitude = user.CurrentLocation?.X,
                LocationUpdatedAt = user.LocationUpdatedAt,
                AccuracyMeters = user.LocationAccuracyMeters,
                AccuracyLevel = LocationHelper.GetAccuracyLevel(user.LocationAccuracyMeters)
            };

            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                dto);
        }

        /// <summary>
        /// Update user's real-time location, optionally log tracking data, and broadcast to active incidents.
        /// </summary>
        public async Task<IServiceResult> UpdateUserLocationAsync(
            Guid userId, double latitude, double longitude, double? accuracyMeters)
        {
            if (userId == Guid.Empty)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0007,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0007));
            }

            var user = await _unitOfWork.Repository<User, Guid>().GetByIdAsync(userId);
            if (user == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
            }

            // Build Point (SRID 4326 = WGS84)
            var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var newLocation = factory.CreatePoint(new Coordinate(longitude, latitude));

            // Update user location fields
            user.CurrentLocation = newLocation;
            user.LocationUpdatedAt = DateTime.UtcNow;
            user.LocationAccuracyMeters = accuracyMeters;
            user.LastActiveAt = DateTime.UtcNow;

            await _unitOfWork.Repository<User, Guid>().UpdateAsync(user);

            // Append tracking log if rescuer has active mission (same UoW, no extra SaveChanges)
            await AppendTrackingLogIfNeededAsync(userId, newLocation, accuracyMeters);

            // Single SaveChanges for both user update and tracking log
            await _unitOfWork.SaveChangesAsync();

            // Publish MediatR event: cache write + SignalR broadcast (handled by LocationUpdatedEventHandler)
            // Exceptions in handler are isolated — DB save already succeeded
            await _publisher.Publish(new LocationUpdatedEvent(
                userId, latitude, longitude, user.LocationUpdatedAt!.Value, accuracyMeters));

            var dto = new UserLocationDto
            {
                Latitude = latitude,
                Longitude = longitude,
                LocationUpdatedAt = user.LocationUpdatedAt,
                AccuracyMeters = accuracyMeters,
                AccuracyLevel = LocationHelper.GetAccuracyLevel(accuracyMeters)
            };

            return new ServiceResult(
                ResultCodeConst.SYS_Success0003,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0003),
                dto);
        }

        /// <summary>
        /// Append tracking log for active rescue missions if sampling criteria met.
        /// Does NOT call SaveChanges — caller is responsible.
        /// </summary>
        private async Task AppendTrackingLogIfNeededAsync(Guid userId, Point newLocation, double? accuracy)
        {
            var activeMissions = await _unitOfWork.Repository<RescueMission, Guid>()
                .GetAllWithSpecAsync(new BaseSpecification<RescueMission>(m =>
                    m.RescuerId == userId
                    && (m.Status == RescueStatus.Accepted || m.Status == RescueStatus.Pending)));

            foreach (var mission in activeMissions)
            {
                // Get last log for this mission
                var lastLogSpec = new BaseSpecification<RescueTrackingLog>(l => l.MissionId == mission.Id);
                lastLogSpec.AddOrderByDescending(l => l.LoggedAt);
                lastLogSpec.ApplyPaging(take: 1, skip: 0);

                var lastLog = await _unitOfWork.Repository<RescueTrackingLog, long>()
                    .GetWithSpecAsync(lastLogSpec);

                if (LocationHelper.ShouldLogTracking(
                    newLocation, lastLog?.Location, lastLog?.LoggedAt, accuracy))
                {
                    await _unitOfWork.Repository<RescueTrackingLog, long>().AddAsync(
                        new RescueTrackingLog
                        {
                            MissionId = mission.Id,
                            RescuerId = userId,
                            Location = newLocation,
                            AccuracyMeters = accuracy,
                            LoggedAt = DateTime.UtcNow
                        });
                }
            }
        }

        #endregion
    }
}