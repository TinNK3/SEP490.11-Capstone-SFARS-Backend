using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFARS.Application.Common;
using SFARS.Application.Configurations;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Dtos.User;
using SFARS.Application.Exceptions;
using SFARS.Application.Utils;
using SFARS.Application.Validations;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;

namespace SFARS.Application.Services.Auth
{
    public class AuthService : IAuthService<AuthUserDto>
    {
        private readonly IUserService<UserDto> _userService;
        private readonly ISystemMessageService _msgService;
        private readonly IRefreshTokenService<RefreshTokenDto> _refreshTokenService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly WebTokenSettings _webTokenSettings;
        private readonly IJwtUtils _jwtUtils;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUserService<UserDto> userService,
            ISystemMessageService msgService,
            IRefreshTokenService<RefreshTokenDto> refreshTokenService,
            IUnitOfWork unitOfWork,
            IJwtUtils jwtUtils,
            IOptionsMonitor<WebTokenSettings> monitor,
            ILogger<AuthService> logger)
        {
            _userService = userService;
            _msgService = msgService;
            _refreshTokenService = refreshTokenService;
            _unitOfWork = unitOfWork;
            _jwtUtils = jwtUtils;
            _webTokenSettings = monitor.CurrentValue;
            _logger = logger;
        }

        public async Task<IServiceResult> SignInWithPasswordAsync(AuthUserDto user)
        {
            // Get user by email 
            var userResult = await _userService.GetByEmailAsync(user.Email);

            // Handle User authentication
            if (userResult.ResultCode == ResultCodeConst.SYS_Success0002
                && userResult.Data is UserDto userDto)
            {
                // Validate password
                if (!ValidatePassword(user.Password, userDto.PasswordHash))
                {
                    _logger.LogWarning("Failed login attempt for {Email}: Invalid password.", user.Email);
                    // Password not match
                    return new ServiceResult(ResultCodeConst.Auth_Warning0007,
                        await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0007));
                }

                // Check if MFA is enabled
                if (userDto.TwoFactorEnabled)
                {
                    _logger.LogInformation("User {UserId} requires 2FA.", userDto.Id);
                    return new ServiceResult(ResultCodeConst.Auth_Warning0010,
                        await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0010));
                }

                // Check if user is active
                if (userDto.Status != UserStatus.Active)
                {
                    _logger.LogWarning("Failed login attempt for {Email}: User status is {Status}.", user.Email, userDto.Status);
                    return new ServiceResult(ResultCodeConst.Auth_Warning0001,
                        await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0001));
                }

                // Map user info to authenticate user
                var authenticateUser = new AuthUserDto
                {
                    Id = userDto.Id,
                    Email = userDto.Email,
                    FirstName = userDto.FirstName,
                    LastName = userDto.LastName,
                    Phone = userDto.Phone,
                    Avatar = userDto.Avatar,
                    Address = userDto.Address,
                    Gender = userDto.Gender,
                    Dob = userDto.Dob,
                    Status = userDto.Status,
                    CreatedAt = userDto.CreatedAt,
                    UpdatedAt = userDto.UpdatedAt,
                    RoleName = userDto.Role ?? UserTypeConstants.User,
                    IsRescuer = userDto.Role == UserTypeConstants.Rescuer
                };

                // Handle authenticate user and generate tokens
                return await AuthenticateUserAsync(authenticateUser);
            }
            else
            {
                // User not found
                _logger.LogWarning("Failed login attempt for {Email}: User not found.", user.Email);
                var message = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002);
                return new ServiceResult(ResultCodeConst.SYS_Warning0002,
                    StringUtils.Format(message, "email"));
            }
        }

        public async Task<IServiceResult> SignUpAsync(AuthUserDto user)
        {
            var validationResult = await ValidateUserInputAsync(user);
            if (validationResult != null && !validationResult.IsValid)
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001),
                    validationResult.ToProblemDetails().Errors);
            }

            // Check if user already exists
            var checkUserAnyResult = await _userService.AnyAsync(u => u.Email.Equals(user.Email));
            if (checkUserAnyResult.Data is true)
            {
                _logger.LogWarning("SignUp failed for {Email}: Email already exists.", user.Email);
                return new ServiceResult(ResultCodeConst.Auth_Warning0006,
                     await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0006));
            }

            // Hash password
            user.PasswordHash = HashUtils.HashPassword(user.Password!);
            // Progress create new user
            user = await CreateNewUserAsync(user) ?? null!;

            if (user != null!) // Create user successfully
            {
                _logger.LogInformation("User created successfully: {Email} ({UserId}).", user.Email, user.Id);
                // TODO: Implement OTP/Email verification for SFARS if needed
                // For now, return success with created user
                return new ServiceResult(ResultCodeConst.SYS_Success0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001),
                    new { UserId = user.Id, Email = user.Email });
            }

            return new ServiceResult(ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }

        // Handle refresh token
        private async Task<IServiceResult> HandleRefreshTokenAsync(AuthUserDto user, string tokenId)
        {
            var getTokenResult = await _refreshTokenService.GetByUserIdAsync(user.Id);

            if (getTokenResult.Data is null)
            {
                return await CreateNewRefreshTokenAsync(user, tokenId);
            }

            return await UpdateExistingRefreshTokenAsync((RefreshTokenDto)getTokenResult.Data, tokenId);
        }

        // Create new refresh token 
        private async Task<IServiceResult> CreateNewRefreshTokenAsync(AuthUserDto user, string tokenId)
        {
            var refreshTokenId = await _jwtUtils.GenerateRefreshTokenAsync();

            var refreshTokenDto = new RefreshTokenDto
            {
                CreateDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddMinutes(_webTokenSettings.RefreshTokenLifeTimeInMinutes),
                RefreshTokenId = refreshTokenId,
                RefreshCount = 0,
                TokenId = tokenId,
                UserId = user.Id
            };

            var result = await _refreshTokenService.CreateAsync(refreshTokenDto);
            if (result.ResultCode != ResultCodeConst.SYS_Success0001)
            {
                var errMsg = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001);
                return new ServiceResult(ResultCodeConst.SYS_Fail0001, StringUtils.Format(errMsg, "refresh token"));
            }

            return new ServiceResult(ResultCodeConst.SYS_Success0001, null, refreshTokenDto);
        }

        // Update existing refresh token
        private async Task<IServiceResult> UpdateExistingRefreshTokenAsync(RefreshTokenDto refreshTokenDto, string tokenId)
        {
            refreshTokenDto.CreateDate = DateTime.UtcNow;
            refreshTokenDto.RefreshTokenId = await _jwtUtils.GenerateRefreshTokenAsync();
            refreshTokenDto.TokenId = tokenId;
            refreshTokenDto.RefreshCount = 0;

            var result = await _refreshTokenService.UpdateAsync(refreshTokenDto.Id, refreshTokenDto);
            if (result.ResultCode != ResultCodeConst.SYS_Success0003)
            {
                return new ServiceResult(ResultCodeConst.SYS_Fail0003,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0003));
            }

            return new ServiceResult(ResultCodeConst.SYS_Success0003, null, refreshTokenDto);
        }

        /// <summary>
        /// Validate password
        /// </summary>
        /// <param name="inputPassword"></param>
        /// <param name="storedHash"></param>
        /// <returns></returns>
        /// <exception cref="UnauthorizedException"></exception>
        private bool ValidatePassword(string? inputPassword, string? storedHash)
        {
            if (string.IsNullOrEmpty(storedHash))
            {
                return false;
            }

            return HashUtils.VerifyPassword(inputPassword ?? string.Empty, storedHash);
        }

        /// <summary>
        /// Validate user input fields
        /// </summary>
        /// <param name="user"></param>
        /// <param name="skipValidation"></param>
        /// <returns></returns>
        /// <exception cref="UnprocessableEntityException"></exception>        
        private async Task<FluentValidation.Results.ValidationResult?> ValidateUserInputAsync(AuthUserDto user, bool skipValidation = false)
        {
            if (!skipValidation)
            {
                return await ValidatorExtensions.ValidateAsync(user);
            }
            return null;
        }

        /// <summary>
        /// Create new User
        /// </summary>
        /// <param name="user"></param>
        /// <param name="createFromExternalProvider"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        private async Task<AuthUserDto?> CreateNewUserAsync(AuthUserDto user,
            bool createFromExternalProvider = false)
        {
            // Not create from external provider, and not provide password
            if (!createFromExternalProvider && string.IsNullOrEmpty(user.Password))
                return null;

            // Get default "User" role for new user
            var userRole = await _unitOfWork.Repository<Role, Guid>()
                .GetWithSpecAsync(new RoleSpecification(UserTypeConstants.User));

            if (userRole == null)
            {
                throw new NotFoundException("Role", UserTypeConstants.User);
            }

            // Create new User entity
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PasswordHash = user.PasswordHash!,
                Phone = user.Phone,
                Avatar = user.Avatar,
                Address = user.Address,
                Gender = user.Gender,
                Dob = user.Dob,
                Status = UserStatus.Active, // New user is active by default (can change to Pending for email verification)
                IsOnline = false,
                CreatedAt = DateTime.UtcNow
            };

            // Add User to database
            await _unitOfWork.Repository<User, Guid>().AddAsync(newUser);

            // Assign role to user (UserRole junction table)
            var newUserRole = new UserRole
            {
                UserId = newUser.Id,
                RoleId = userRole.Id,
                AssignedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<UserRole, Guid>().AddAsync(newUserRole);

            // Save all changes with transaction to ensure data consistency
            if (await _unitOfWork.SaveChangesWithTransactionAsync() > 0)
            {
                // Return AuthenticateUserDto with populated data
                user.Id = newUser.Id;
                user.Status = UserStatus.Active;
                user.CreatedAt = newUser.CreatedAt;
                user.RoleName = userRole.RoleName;
                user.IsRescuer = userRole.RoleName == UserTypeConstants.Rescuer;
                return user;
            }

            return null;
        }

        /// <summary>
        /// AuthenticateUserAsync
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<ServiceResult> AuthenticateUserAsync(AuthUserDto? user)
        {
            // Check not exist authenticate user (save fail,...) 
            if (user == null)
                return new ServiceResult(ResultCodeConst.SYS_Fail0001, "Unknown error invoke while authenticating user.");

            // Validate user status
            if (user.Id == Guid.Empty || string.IsNullOrEmpty(user.RoleName))
            {
                return new ServiceResult(ResultCodeConst.Auth_Warning0007,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0007));
            }

            if (user.Status != UserStatus.Active)
            {
                return new ServiceResult(ResultCodeConst.Auth_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0001));
            }

            // Generate token
            var tokenId = Guid.NewGuid().ToString();
            var jwtResponse = await _jwtUtils.GenerateJwtTokenAsync(
                tokenId: tokenId, user: user);

            if (string.IsNullOrEmpty(jwtResponse.AccessToken) || jwtResponse.ValidTo <= DateTime.UtcNow)
            {
                 _logger.LogError("Failed to generate JWT token for User {UserId}.", user.Id);
                 return new ServiceResult(ResultCodeConst.SYS_Fail0001, "Invalid JWT token generated");
            }

            _logger.LogInformation("User {UserId} authenticated. Token generated.", user.Id);

            // Handle refresh token
            var refreshTokenResult = await HandleRefreshTokenAsync(user, tokenId);
            if (refreshTokenResult.Data is not RefreshTokenDto refreshTokenDto)
            {
                return new ServiceResult(refreshTokenResult.ResultCode, refreshTokenResult.Message);
            }

            // Create AuthResult with Tokens and User Info
            var authResult = new AuthResultDto
            {
                AccessToken = jwtResponse.AccessToken,
                RefreshToken = refreshTokenDto.RefreshTokenId,
                ValidTo = jwtResponse.ValidTo,
                User = user.ToUserDto()
            };

            return new ServiceResult(
                ResultCodeConst.Auth_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.Auth_Success0002),
                authResult);
        }
    }
}