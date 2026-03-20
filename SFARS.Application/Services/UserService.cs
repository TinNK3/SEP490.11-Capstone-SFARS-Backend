using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.User;
using SFARS.Application.Events;
using SFARS.Application.Utils;
using SFARS.Application.Validations;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.Params;
using SFARS.Domain.Specifications.Users;
using SFARS.Infrastructure.Helpers;
using System.Text.Json;

namespace SFARS.Application.Services
{
    public class UserService : GenericService<User, UserDto, Guid>, IUserService<UserDto>
    {
        private readonly IPublisher _publisher;
        private readonly IAdminAuditLogService _auditLogService;

        public UserService(
            ISystemMessageService msgService,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<UserService> logger,
            IPublisher publisher,
            IAdminAuditLogService auditLogService) : base(msgService, unitOfWork, mapper, logger)
        {
            _publisher = publisher;
            _auditLogService = auditLogService;
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

            var spec = UserSpecification.ById(userId);
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
        /// [Admin] Get a single user by their ID.
        /// </summary>
        public async Task<IServiceResult> GetUserByIdAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0007,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0007)
                );
            }

            var spec = UserSpecification.ById(userId);
            var user = await _unitOfWork.Repository<User, Guid>().GetWithSpecAsync(spec);

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

            var spec = UserSpecification.ById(userId);
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
        /// Update user's real-time location.
        /// High-Performance implementation: defer DB writes unless moved > 250m or 3 mins passed to avoid IOPS spike.
        /// Always publishes to MediatR so SignalR and Redis update instantly for Real-time tracking.
        /// RescueTrackingLog is appended via AppendTrackingLogIfNeededAsync using the same throttling rules.
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

            // Publish MediatR event: cache write + SignalR broadcast (handled by LocationUpdatedEventHandler)
            // This guarantees the FE always gets real-time sockets regardless of whether SQL DB was hit
            var newUpdateAt = DateTime.UtcNow;
            await _publisher.Publish(new LocationUpdatedEvent(
                userId, latitude, longitude, newUpdateAt, accuracyMeters));

            // Only hit SQL if the user actually shifted > 50 meters from DB record OR >= 2 minutes passed since last DB record
            bool shouldSyncDb = false;
            
            if (user.CurrentLocation == null || user.LocationUpdatedAt == null) 
            {
                shouldSyncDb = true; // First time writing
            }
            else 
            {
                var timeSinceLastDbSave = newUpdateAt - user.LocationUpdatedAt.Value;
                var distanceMoved = user.CurrentLocation.Distance(newLocation);

                // Roles differ in frequency: Rescuers move fast (hit distance block), Victims stationary (hit time block)
                if (timeSinceLastDbSave.TotalMinutes >= 2 || distanceMoved > 50)
                {
                    shouldSyncDb = true;
                }
            }

            if (shouldSyncDb)
            {
                // Update user location fields in Entity
                user.CurrentLocation = newLocation;
                user.LocationUpdatedAt = newUpdateAt;
                user.LocationAccuracyMeters = accuracyMeters;
                user.LastActiveAt = newUpdateAt;

                await _unitOfWork.Repository<User, Guid>().UpdateAsync(user);

                // Append tracking log if rescuer has active mission (same UoW, no extra SaveChanges)
                await AppendTrackingLogIfNeededAsync(userId, newLocation, accuracyMeters);

                // Single SaveChanges for both user update and tracking log
                await _unitOfWork.SaveChangesAsync();
            }

            var dto = new UserLocationDto
            {
                Latitude = latitude,
                Longitude = longitude,
                LocationUpdatedAt = newUpdateAt, // Represents memory time, not necessarily DB time
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

        #region Admin

        /// <summary>
        /// [Admin] Returns a paginated, filtered list of all users.
        /// Filters: role name, status, registration date range, free-text search.
        /// </summary>
        public async Task<IServiceResult> GetAllUsersAsync(UserSpecParams specParams)
        {
            specParams ??= new UserSpecParams();
            var page = specParams.GetPage();
            var limit = specParams.GetTake();

            try
            {
                var countSpec = UserSpecification.Count(specParams);
                var totalItems = await _unitOfWork.Repository<User, Guid>().CountAsync(countSpec);

                if (totalItems == 0)
                {
                    return new ServiceResult(
                        ResultCodeConst.SYS_Warning0004,
                        await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004),
                        new PaginatedResultDto<UserDto>(
                            Enumerable.Empty<UserDto>(), page, limit, 0, 0));
                }

                var spec = UserSpecification.List(specParams);
                var users = await _unitOfWork.Repository<User, Guid>().GetAllWithSpecAsync(spec, tracked: false);
                var dtos = _mapper.Map<IEnumerable<UserDto>>(users);
                var totalPages = (int)Math.Ceiling((double)totalItems / limit);

                return new ServiceResult(
                    ResultCodeConst.SYS_Success0002,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                    new PaginatedResultDto<UserDto>(dtos, page, limit, totalPages, totalItems));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user list");
                throw;
            }
        }

        /// <summary>
        /// [Admin] Update the status of any user account.
        /// Logs the optional reason server-side.
        /// </summary>
        public async Task<IServiceResult> UpdateUserStatusAsync(
            Guid adminId, Guid targetUserId, UserStatus newStatus, string? reason)
        {
            if (targetUserId == Guid.Empty)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0007,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0007));
            }

            var user = await _unitOfWork.Repository<User, Guid>().GetByIdAsync(targetUserId);

            if (user == null)
            {
                return new ServiceResult(
                    ResultCodeConst.Admin_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.Admin_Warning0001));
            }

            if (adminId == targetUserId)
            {
                return new ServiceResult(
                    ResultCodeConst.Admin_Warning0002,
                    await _msgService.GetMessageAsync(ResultCodeConst.Admin_Warning0002));
            }

            var previous = user.Status;
            user.Status = newStatus;

            await _unitOfWork.Repository<User, Guid>().UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            // Audit log
            await _auditLogService.LogAsync(
                adminId,
                AdminAction.UpdateUserStatus,
                "User",
                targetUserId,
                JsonSerializer.Serialize(new { Status = previous.ToString() }),
                JsonSerializer.Serialize(new { Status = newStatus.ToString() }),
                reason);

            return new ServiceResult(
                ResultCodeConst.Admin_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.Admin_Success0002));
        }

        /// <summary>
        /// [Admin] Create a new user account and assign the given role.
        /// Password is hashed before storage. Status defaults to Active.
        /// </summary>
        public async Task<IServiceResult> CreateUserAsync(Guid adminId, UserDto dto)
        {
            // 1. Duplicate email check
            var emailTaken = await _unitOfWork.Repository<User, Guid>()
                .AnyAsync(u => u.Email == dto.Email);

            if (emailTaken)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0006,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0006));
            }

            // 2. Lookup role by enum name ("User" | "Rescuer" | "Admin")
            var roleName   = dto.Role ?? RoleType.User.ToString();
            var roleEntity = await _unitOfWork.Repository<Role, Guid>()
                .GetWithSpecAsync(new RoleSpecification(roleName));

            if (roleEntity == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
            }

            // 3. Build user entity
            var newUser = new User
            {
                Id           = Guid.NewGuid(),
                FirstName    = dto.FirstName,
                LastName     = dto.LastName,
                Email        = dto.Email,
                PasswordHash = HashUtils.HashPassword(dto.Password!),
                Phone        = dto.Phone,
                Status       = UserStatus.Active,
                IsOnline     = false,
                CreatedAt    = DateTime.UtcNow
            };

            await _unitOfWork.Repository<User, Guid>().AddAsync(newUser);

            // 4. Assign role
            await _unitOfWork.Repository<UserRole, Guid>().AddAsync(new UserRole
            {
                UserId     = newUser.Id,
                RoleId     = roleEntity.Id,
                AssignedAt = DateTime.UtcNow
            });

            // 5. Persist with transaction
            if (await _unitOfWork.SaveChangesWithTransactionAsync() > 0)
            {
                var createdDto = _mapper.Map<UserDto>(newUser);
                createdDto.Role = roleEntity.RoleName;

                // Audit log
                await _auditLogService.LogAsync(
                    adminId,
                    AdminAction.CreateUser,
                    "User",
                    newUser.Id,
                    null,
                    JsonSerializer.Serialize(new
                    {
                        Email     = newUser.Email,
                        FirstName = newUser.FirstName,
                        LastName  = newUser.LastName,
                        Role      = roleEntity.RoleName,
                        Status    = newUser.Status.ToString()
                    }));

                return new ServiceResult(
                    ResultCodeConst.SYS_Success0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001),
                    createdDto);
            }

            return new ServiceResult(
                ResultCodeConst.SYS_Fail0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }

        #endregion
    }
}