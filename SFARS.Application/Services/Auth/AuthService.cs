using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using SFARS.Application.Common;
using SFARS.Application.Configurations;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Dtos.User;
using SFARS.Application.Exceptions;
using SFARS.Application.Utils;
using SFARS.Application.Validations;
using SFARS.Application.Validations.Auth;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Models;
using SFARS.Domain.Specifications;
using System.IdentityModel.Tokens.Jwt;

namespace SFARS.Application.Services.Auth
{
    public class AuthService : IAuthService<AuthUserDto>
    {
        private readonly IUserService<UserDto> _userService;
        private readonly ISystemMessageService _msgService;
        private readonly IRefreshTokenService<RefreshTokenDto> _refreshTokenService;
        private readonly TokenValidationParameters _tokenValidationParameters;
        private readonly IUnitOfWork _unitOfWork;
        private readonly WebTokenSettings _webTokenSettings;
        private readonly IJwtUtils _jwtUtils;
        private readonly IExternalAuthService _externalAuthService;
        private readonly ILogger<AuthService> _logger;
        private readonly IEmailService _emailService;
        private readonly ITokenBlacklistService _tokenBlacklistService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthService(
            IUserService<UserDto> userService,
            ISystemMessageService msgService,
            IRefreshTokenService<RefreshTokenDto> refreshTokenService,
            TokenValidationParameters tokenValidationParameters,
            IUnitOfWork unitOfWork,
            IJwtUtils jwtUtils,
            IOptionsMonitor<WebTokenSettings> monitor,
            ILogger<AuthService> logger,
            IExternalAuthService externalAuthService,
            IEmailService emailService,
            ITokenBlacklistService tokenBlacklistService,
            IHttpContextAccessor httpContextAccessor)
        {
            _userService = userService;
            _msgService = msgService;
            _refreshTokenService = refreshTokenService;
            _tokenValidationParameters = tokenValidationParameters;
            _unitOfWork = unitOfWork;
            _jwtUtils = jwtUtils;
            _webTokenSettings = monitor.CurrentValue;
            _logger = logger;
            _externalAuthService = externalAuthService;
            _emailService = emailService;
            _tokenBlacklistService = tokenBlacklistService;
            _httpContextAccessor = httpContextAccessor;
        }

        #region Sign-In

        public async Task<IServiceResult> SignInAsync(string email)
        {
            AuthUserDto? authUser = null;
            bool isAdmin = false;

            // Get user by email
            var userResult = await _userService.GetByEmailAsync(email);

            if (userResult == null || userResult.ResultCode != ResultCodeConst.SYS_Success0002)
            {
                var message = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002);
                return new ServiceResult(ResultCodeConst.SYS_Warning0002,
                    StringUtils.Format(message, "email"));
            }
            else if (userResult.Data is UserDto userDto)
            {
                //Map user info to authenticate user
                authUser = userDto.ToAuthUserDto();

                // Check whether user is admin
                isAdmin = userDto.Role == UserTypeConstants.Admin;
            }

            if (authUser != null)
            {
                var userTypeResult = new UserTypeResultDto
                {
                    UserType = isAdmin ? UserTypeConstants.Admin //Admin user
                    : authUser.IsRescuer ? UserTypeConstants.Rescuer //Rescuer user
                    : UserTypeConstants.User //Regular user
                };

                //Check account haven't password yet
                var hasPassword = !string.IsNullOrEmpty(authUser.PasswordHash);

                // Check account status
                if (authUser.Status != UserStatus.Active)
                {
                    return new ServiceResult(ResultCodeConst.Auth_Warning0001,
                            await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0001));
                }

                //Response to keep on sign-in with username/password
                if (hasPassword) //Existing user with password
                {
                    return new ServiceResult(ResultCodeConst.Auth_Success0001,
                        await _msgService.GetMessageAsync(ResultCodeConst.Auth_Success0001),
                        new SignInMethodDto { Method = "password" });
                }

                // Response to keep on sign-in with OTP
                // since user sign-up with external provider
                else
                {
                    // Use unified SendOtpAsync with SignIn type
                    var sendResult = await SendOtpAsync(email, OtpType.SignIn);
                    if (sendResult.ResultCode == ResultCodeConst.Auth_Success0005)
                    {
                        return new ServiceResult(ResultCodeConst.Auth_Success0005,
                            await _msgService.GetMessageAsync(ResultCodeConst.Auth_Success0005),
                            new SignInMethodDto { Method = "otp" });
                    }
                    else
                    {
                        return sendResult;
                    }
                }
            }

            // Unknown error
            return new ServiceResult(ResultCodeConst.SYS_Fail0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0002));
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

        public async Task<IServiceResult> SignInWithGoogleAsync(string googleIdToken)
        {
            var validator = new SignInWithGoogleValidator();
            var validation = await validator.ValidateAsync(googleIdToken);
            if (!validation.IsValid)
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001),
                    validation.Errors.Select(e => e.ErrorMessage));
            }

            // Verify Google Token (Using Infrastructure Service)
            var externalUser = await _externalAuthService.VerifyGoogleTokenAsync(googleIdToken);

            // Check if user exists
            var userResult = await _userService.GetByEmailAsync(externalUser.Email);

            if (userResult.ResultCode == ResultCodeConst.SYS_Success0002
                && userResult.Data is UserDto userDto)
            {
                // User exists: Check status
                if (userDto.Status != UserStatus.Active)
                {
                    _logger.LogWarning("Failed Google login for {Email}: User status is {Status}.", externalUser.Email, userDto.Status);
                    return new ServiceResult(ResultCodeConst.Auth_Warning0001,
                        await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0001));
                }

                // Map to AuthUserDto
                var authenticateUser = userDto.ToAuthUserDto();
                return await AuthenticateUserAsync(authenticateUser);
            }
            else
            {
                // User does not exist: Auto Register
                var newUser = new AuthUserDto
                {
                    Email = externalUser.Email,
                    FirstName = externalUser.FirstName ?? "User",
                    LastName = externalUser.LastName ?? "",
                    Avatar = externalUser.Avatar,
                    Status = UserStatus.Active
                };

                // Create new user (createFromExternalProvider = true)
                var createdUser = await CreateNewUserAsync(newUser, createFromExternalProvider: true);

                if (createdUser != null)
                {
                    _logger.LogInformation("New user registered via Google: {Email}", externalUser.Email);
                    return await AuthenticateUserAsync(createdUser);
                }

                return new ServiceResult(ResultCodeConst.SYS_Fail0001,
                     await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
            }
        }

        public async Task<IServiceResult> SignInWithOtpAsync(string otp, AuthUserDto user)
        {
            // Verify OTP using the unified VerifyOtpAsync with SignIn type
            var verifyResult = await VerifyOtpAsync(user.Email, otp, OtpType.SignIn);
            if (verifyResult.ResultCode != ResultCodeConst.Auth_Success0010)
            {
                return verifyResult;
            }

            // Get user by email for authentication
            var userResult = await _userService.GetByEmailAsync(user.Email);

            if (userResult.ResultCode == ResultCodeConst.SYS_Success0002
                && userResult.Data is UserDto userDto)
            {
                user = new AuthUserDto
                {
                    Id = userDto.Id,
                    Email = userDto.Email,
                    FirstName = userDto.FirstName ?? string.Empty,
                    LastName = userDto.LastName ?? string.Empty,
                    Phone = userDto.Phone,
                    Dob = userDto.Dob,
                    Avatar = userDto.Avatar,
                    Address = userDto.Address,
                    Status = userDto.Status,
                    IsRescuer = userDto.Role == UserTypeConstants.Rescuer,
                    RoleName = userDto.Role ?? UserTypeConstants.User,
                    Password = string.Empty
                };

                return await AuthenticateUserAsync(user);
            }
            else
            {
                var message = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002);
                return new ServiceResult(ResultCodeConst.SYS_Warning0002,
                    StringUtils.Format(message, "email"));
            }
        }

        #endregion

        #region Sign-Up

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

        #endregion

        #region Password Management

        public async Task<IServiceResult> ForgotPasswordAsync(string email)
        {
            // Delegate to unified SendOtpAsync with ResetPassword type
            return await SendOtpAsync(email, OtpType.ResetPassword);
        }

        public async Task<IServiceResult> ResetPasswordAsync(string email, string otp, string newPassword)
        {
            // Validate using DTO
            var dto = new ResetPasswordDto { Email = email, Otp = otp, NewPassword = newPassword };
            var validation = await ValidatorExtensions.ValidateAsync(dto);
            if (validation != null && !validation.IsValid)
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001),
                    validation.ToProblemDetails().Errors);
            }

            // Get user by email
            var userResult = await _userService.GetByEmailAsync(email);

            if (userResult.ResultCode != ResultCodeConst.SYS_Success0002 || userResult.Data is not UserDto userDto)
            {
                _logger.LogWarning("Reset password attempt for non-existent email: {Email}", email);
                return new ServiceResult(ResultCodeConst.SYS_Warning0002,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));
            }

            // Find OtpRequest with ResetPassword type
            var otpRequest = (await _unitOfWork.Repository<OtpRequest, Guid>()
                .GetAllAsync())
                .Where(o => o.UserId == userDto.Id
                    && o.Type == OtpType.ResetPassword
                    && !o.IsUsed)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefault();

            if (otpRequest == null)
            {
                _logger.LogWarning("Reset password attempt: no OTP request found for {Email}", email);
                return new ServiceResult(ResultCodeConst.Auth_Warning0017,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0017));
            }

            // Check expiration
            if (otpRequest.ExpiredAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Reset password attempt with expired OTP for {Email}", email);
                return new ServiceResult(ResultCodeConst.Auth_Warning0014,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0014));
            }

            // Check attempt limit
            if (otpRequest.AttemptCount >= OtpConstants.MaxAttempts)
            {
                _logger.LogWarning("Reset password attempt: OTP locked for {Email}", email);
                return new ServiceResult(ResultCodeConst.Auth_Warning0015,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0015));
            }

            // Verify OTP code
            if (otpRequest.Code != otp)
            {
                // Increment attempt count
                otpRequest.AttemptCount++;
                otpRequest.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Repository<OtpRequest, Guid>().UpdateAsync(otpRequest);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogWarning("Reset password attempt with invalid OTP for {Email} (attempt {Attempt}/{Max})",
                    email, otpRequest.AttemptCount, OtpConstants.MaxAttempts);
                return new ServiceResult(ResultCodeConst.Auth_Warning0005,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0005));
            }

            // Check if new password is same as old password
            if (!string.IsNullOrEmpty(userDto.PasswordHash) && ValidatePassword(newPassword, userDto.PasswordHash))
            {
                _logger.LogWarning("Reset password attempt with same password for {Email}", email);
                return new ServiceResult(ResultCodeConst.Auth_Warning0011,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0011));
            }

            // Hash new password
            var newPasswordHash = HashUtils.HashPassword(newPassword);

            // Update user password
            var updateResult = await _userService.UpdatePasswordAsync(userDto.Id, newPasswordHash);

            if (updateResult.ResultCode == ResultCodeConst.SYS_Success0003)
            {
                // Mark OTP as used
                otpRequest.IsUsed = true;
                otpRequest.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Repository<OtpRequest, Guid>().UpdateAsync(otpRequest);

                // Invalidate all remaining ResetPassword OTPs for this user
                var remainingOtps = (await _unitOfWork.Repository<OtpRequest, Guid>()
                    .GetAllAsync())
                    .Where(o => o.UserId == userDto.Id
                        && o.Type == OtpType.ResetPassword
                        && !o.IsUsed
                        && o.Id != otpRequest.Id)
                    .ToList();

                foreach (var remainingOtp in remainingOtps)
                {
                    remainingOtp.IsUsed = true;
                    remainingOtp.UpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.Repository<OtpRequest, Guid>().UpdateAsync(remainingOtp);
                }

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Password reset successful for {Email}", email);
                return new ServiceResult(ResultCodeConst.SYS_Success0003,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0003));
            }

            _logger.LogError("Failed to reset password for {Email}", email);
            return new ServiceResult(ResultCodeConst.SYS_Fail0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }

        #endregion

        #region Token Management

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


        public async Task<IServiceResult> RefreshTokenAsync(string refreshTokenId, string accessToken)
        {
            //Try to validate and extract claims from access token
            var token = _jwtUtils.GetPrincipalFromExpiredToken(accessToken);
            if (token == null)
            {
                _logger.LogWarning("Refresh token failed: unable to extract principal from access token.");
                throw new UnauthorizedException("Invalid access token.");
            }

            // Retrieve claims from the authenticated user's identity
            var roleName = token?.Claims.FirstOrDefault(c => c.Type == CustomClaimTypes.Role)?.Value
                           ?? token?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            var userType = token?.Claims.FirstOrDefault(c => c.Type == CustomClaimTypes.UserType)?.Value;
            var email = token?.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value
                        ?? token?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
                        ?? token?.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            var name = token?.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value
                       ?? token?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var tokenId = token?.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
            var userIdClaim = token?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
                              ?? token?.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                              ?? token?.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
            var hasUserId = Guid.TryParse(userIdClaim, out var userId);
            if (string.IsNullOrEmpty(email) // Is not exist email claim
                || string.IsNullOrEmpty(userType) // Is not exist user type claim
                || string.IsNullOrEmpty(roleName) // Is not exist role claim
                || string.IsNullOrEmpty(name) // Is not exist name claim
                || string.IsNullOrEmpty(tokenId) // Is not exist tokenId claim
                || !hasUserId) // Is not exist user id claim
            {
                _logger.LogWarning(
                    "Refresh token failed: missing claims. Email:{Email}, UserType:{UserType}, Role:{Role}, Name:{Name}, Jti:{Jti}, UserId:{UserId}",
                    email, userType, roleName, name, tokenId, userIdClaim);
                // 401
                throw new UnauthorizedException("Missing token claims.");
            }

            var getRefreshTokenResult = await _refreshTokenService.GetByTokenIdAndRefreshTokenIdAsync(
                tokenId, refreshTokenId);
             if (getRefreshTokenResult.Data != null) // Exist refresh token
            {
                // Map to RefreshTokenDto
                var refreshTokenDto = (getRefreshTokenResult.Data as RefreshTokenDto)!;
                // Retrieve refresh token limit
                var maxRefreshTokenLifeSpan = _webTokenSettings.MaxRefreshTokenLifeSpan;
                // Check whether valid refresh token limit
                if (refreshTokenDto.RefreshCount + 1 > maxRefreshTokenLifeSpan)
                {
                    throw new ForbiddenException(
                        await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0002));
                }

                // Generate new tokenId
                tokenId = Guid.NewGuid().ToString();
                // Rotate refresh token
                refreshTokenDto.TokenId = tokenId;
                refreshTokenDto.RefreshTokenId = await _jwtUtils.GenerateRefreshTokenAsync();
                refreshTokenDto.RefreshCount += 1;
                refreshTokenDto.CreateDate = DateTime.UtcNow;
                refreshTokenDto.ExpiryDate = DateTime.UtcNow.AddMinutes(_webTokenSettings.RefreshTokenLifeTimeInMinutes);

                // Progress update
                var updateResult = await _refreshTokenService.UpdateAsync(refreshTokenDto.Id, refreshTokenDto);
                if (updateResult.ResultCode == ResultCodeConst.SYS_Success0003) // Update success
                {
                    UserDto? userDto = null;
                    var userResult = await _userService.GetByEmailAsync(email);
                    if (userResult?.ResultCode == ResultCodeConst.SYS_Success0002
                        && userResult.Data is UserDto foundUser)
                    {
                        userDto = foundUser;
                    }
                    else
                    {
                        userDto = new UserDto
                        {
                            Id = userId,
                            Email = email,
                            FirstName = name,
                            LastName = string.Empty,
                            Status = UserStatus.Active,
                            Role = roleName
                        };
                    }

                    // Generate authenticated user
                    var authenticatedUserDto = new AuthUserDto()
                    {
                        Id = userId,
                        Email = email,
                        FirstName = name,
                        LastName = string.Empty,
                        RoleName = roleName,
                        IsRescuer = userType.Equals(ClaimValues.RESCUER_CLAIMVALUE),
                        Status = UserStatus.Active
                    };

                    // Generate access token
                    var generateResult = await _jwtUtils
                        .GenerateJwtTokenAsync(tokenId: tokenId, user: authenticatedUserDto);

                    return new ServiceResult(ResultCodeConst.Auth_Success0008,
                        await _msgService.GetMessageAsync(ResultCodeConst.Auth_Success0008),
                        new AuthResultDto
                        {
                            AccessToken = generateResult.AccessToken,
                            RefreshToken = refreshTokenDto.RefreshTokenId,
                            ValidTo = generateResult.ValidTo,
                            User = userDto
                        });
                }
            }

            return null!;
        }

        // Update existing refresh token
        private async Task<IServiceResult> UpdateExistingRefreshTokenAsync(RefreshTokenDto refreshTokenDto, string tokenId)
        {
            refreshTokenDto.CreateDate = DateTime.UtcNow;
            refreshTokenDto.ExpiryDate = DateTime.UtcNow.AddMinutes(_webTokenSettings.RefreshTokenLifeTimeInMinutes);
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

        #endregion

        #region Private Helpers

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
                PasswordHash = user.PasswordHash ?? string.Empty,
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

            // Domain Audit: Record Login History
            var httpContext = _httpContextAccessor.HttpContext;
            var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
            var userAgent = httpContext?.Request?.Headers != null 
                ? httpContext.Request.Headers["User-Agent"].ToString() 
                : null;

            await _unitOfWork.Repository<UserLoginHistory, long>().AddAsync(new UserLoginHistory
            {
                UserId = user.Id,
                LoginAt = DateTime.UtcNow,
                IPAddress = ipAddress,
                UserAgent = userAgent
            });
            await _unitOfWork.SaveChangesAsync();

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

        // Send OTP Email (legacy helper - kept for backward compatibility)
        public async Task<bool> SendAndSaveOtpAsync(string otpCode, AuthUserDto user,
            string subject, string emailBody)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogWarning("Cannot send OTP: missing user email.");
                return false;
            }

            // Process send email
            var emailMessageDto = new EmailMessageDto
            {
                To = user.Email,
                Subject = subject,
                Body = StringUtils.Format(emailBody, otpCode)
            };

            var sendResult = await _emailService.SendEmailAsync(message: emailMessageDto, isBodyHtml: true);
            if (!sendResult)
            {
                _logger.LogWarning("Failed to send OTP email to {Email}.", user.Email);
                return false;
            }

            return true;
        }

        #endregion

        #region OTP Management

        /// <summary>
        /// Send OTP - unified endpoint for both SignIn and ResetPassword types
        /// </summary>
        public async Task<IServiceResult> SendOtpAsync(string email, OtpType type)
        {
            // Validate using DTO
            var dto = new SendOtpDto { Email = email, Type = type };
            var validation = await ValidatorExtensions.ValidateAsync(dto);
            if (validation != null && !validation.IsValid)
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001),
                    validation.ToProblemDetails().Errors);
            }

            // Get user by email
            var userResult = await _userService.GetByEmailAsync(email);

            if (userResult.ResultCode != ResultCodeConst.SYS_Success0002 || userResult.Data is not UserDto userDto)
            {
                _logger.LogWarning("Send OTP request for non-existent email: {Email}", email);
                return new ServiceResult(ResultCodeConst.SYS_Warning0002,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));
            }

            // Check if user is active
            if (userDto.Status != UserStatus.Active)
            {
                _logger.LogWarning("Send OTP request for inactive user: {Email}", email);
                return new ServiceResult(ResultCodeConst.Auth_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0001));
            }

            // Check lockout: find most recent OTP with max attempts reached within lockout period
            var lockedOtp = (await _unitOfWork.Repository<OtpRequest, Guid>()
                .GetAllAsync())
                .Where(o => o.UserId == userDto.Id
                    && o.Type == type
                    && o.AttemptCount >= OtpConstants.MaxAttempts
                    && o.CreatedAt.AddMinutes(OtpConstants.LockoutMinutes) > DateTime.UtcNow)
                .FirstOrDefault();

            if (lockedOtp != null)
            {
                _logger.LogWarning("Send OTP: locked due to too many attempts for {Email} ({Type})", email, type);
                return new ServiceResult(ResultCodeConst.Auth_Warning0015,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0015));
            }

            // Check cooldown: find most recent OTP within cooldown period
            var recentOtp = (await _unitOfWork.Repository<OtpRequest, Guid>()
                .GetAllAsync())
                .Where(o => o.UserId == userDto.Id
                    && o.Type == type
                    && o.CreatedAt.AddSeconds(OtpConstants.CooldownSeconds) > DateTime.UtcNow)
                .FirstOrDefault();

            if (recentOtp != null)
            {
                _logger.LogWarning("Send OTP: cooldown not expired for {Email} ({Type})", email, type);
                return new ServiceResult(ResultCodeConst.Auth_Warning0016,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0016));
            }

            // Invalidate all existing OTPs for this user + type
            var existingOtps = (await _unitOfWork.Repository<OtpRequest, Guid>()
                .GetAllAsync())
                .Where(o => o.UserId == userDto.Id
                    && o.Type == type
                    && !o.IsUsed)
                .ToList();

            foreach (var existingOtp in existingOtps)
            {
                existingOtp.IsUsed = true;
                existingOtp.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Repository<OtpRequest, Guid>().UpdateAsync(existingOtp);
            }

            // Generate OTP code
            var otpCode = StringUtils.GenerateUniqueCode();

            // Create new OtpRequest
            var otpRequest = new OtpRequest
            {
                Id = Guid.NewGuid(),
                UserId = userDto.Id,
                Code = otpCode,
                Type = type,
                ExpiredAt = DateTime.UtcNow.AddMinutes(OtpConstants.OtpExpirationMinutes),
                IsUsed = false,
                AttemptCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<OtpRequest, Guid>().AddAsync(otpRequest);
            await _unitOfWork.SaveChangesAsync();

            // Map to AuthUserDto for email
            var authUser = userDto.ToAuthUserDto();

            // Build email template based on type
            string emailSubject;
            string emailBody;

            if (type == OtpType.ResetPassword)
            {
                bool isSetPassword = string.IsNullOrEmpty(userDto.PasswordHash);
                string title = isSetPassword ? "Thiết Lập Mật Khẩu" : "Đặt Lại Mật Khẩu";
                string actionText = isSetPassword ? "thiết lập mật khẩu" : "đặt lại mật khẩu";
                string warningActionText = isSetPassword ? "thiết lập mật khẩu" : "tác vụ này";
                string warningSuffix = isSetPassword ? "." : " và đổi mật khẩu ngay lập tức.";

                emailSubject = isSetPassword ? "Set Password OTP for SFARS" : "Password Reset OTP for SFARS";
                emailBody = $@"
                    <div style='font-family: Arial, sans-serif; background:#f6f7fb; padding:24px;'>
                        <div style='max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;overflow:hidden;'>
                            <div style='background:#C0392B;color:#fff;padding:16px 24px;'>
                                <h2 style='margin:0;font-size:20px;'>⚠️ SFARS - Yêu Cầu {title}</h2>
                            </div>
                            <div style='padding:24px;color:#333;line-height:1.6;'>
                                <p>Xin chào <strong>{authUser.FirstName} {authUser.LastName}</strong>,</p>
                                <p>Chúng tôi nhận được yêu cầu <strong>{actionText}</strong> cho tài khoản của bạn. Đây là mã OTP:</p>
                                <div style='text-align:center;margin:20px 0;'>
                                    <span style='display:inline-block;background:#fdf2f2;color:#C0392B;
                                        font-size:28px;letter-spacing:6px;padding:12px 18px;border-radius:10px;border:2px solid #C0392B;'>
                                        {otpCode}
                                    </span>
                                </div>
                                <p>Mã có hiệu lực trong <strong>{OtpConstants.OtpExpirationMinutes} phút</strong>. Vui lòng không chia sẻ mã này với bất kỳ ai.</p>
                                <div style='background:#fdf2f2;border-left:4px solid #C0392B;padding:12px;margin:16px 0;border-radius:4px;'>
                                    <p style='margin:0;color:#C0392B;'><strong>⚠️ Cảnh báo bảo mật:</strong> Nếu bạn không yêu cầu {warningActionText}, vui lòng bỏ qua email này{warningSuffix}</p>
                                </div>
                                <p style='margin-top:24px;'>Cảm ơn bạn đã sử dụng SFARS.</p>
                            </div>
                        </div>
                    </div>";
            }
            else // SIGN_IN
            {
                emailSubject = "Your One-Time Password (OTP) for SFARS Sign-In";
                emailBody = $@"
                    <div style='font-family: Arial, sans-serif; background:#f6f7fb; padding:24px;'>
                        <div style='max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;overflow:hidden;'>
                            <div style='background:#2C3E50;color:#fff;padding:16px 24px;'>
                                <h2 style='margin:0;font-size:20px;'>SFARS Verification</h2>
                            </div>
                            <div style='padding:24px;color:#333;line-height:1.6;'>
                                <p>Xin chào <strong>{authUser.FirstName} {authUser.LastName}</strong>,</p>
                                <p>Đây là mã OTP để đăng nhập:</p>
                                <div style='text-align:center;margin:20px 0;'>
                                    <span style='display:inline-block;background:#f0f2f7;color:#2C3E50;
                                        font-size:28px;letter-spacing:6px;padding:12px 18px;border-radius:10px;'>
                                        {otpCode}
                                    </span>
                                </div>
                                <p>Mã có hiệu lực trong <strong>{OtpConstants.OtpExpirationMinutes} phút</strong>. Vui lòng không chia sẻ mã này.</p>
                                <p style='margin-top:24px;'>Cảm ơn bạn đã sử dụng SFARS.</p>
                            </div>
                        </div>
                    </div>";
            }

            // Send OTP email
            var isOtpSent = await SendAndSaveOtpAsync(otpCode, authUser, emailSubject, emailBody);

            if (isOtpSent)
            {
                _logger.LogInformation("OTP sent to {Email} for {Type}", email, type);
                return new ServiceResult(ResultCodeConst.Auth_Success0005,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Success0005));
            }

            _logger.LogError("Failed to send OTP to {Email} for {Type}", email, type);
            return new ServiceResult(ResultCodeConst.Auth_Fail0002,
                await _msgService.GetMessageAsync(ResultCodeConst.Auth_Fail0002));
        }

        /// <summary>
        /// Verify OTP - validates OTP code against OtpRequest table with type isolation
        /// </summary>
        public async Task<IServiceResult> VerifyOtpAsync(string email, string otp, OtpType type)
        {
            // Validate using DTO
            var dto = new VerifyOtpDto { Email = email, Otp = otp, Type = type };
            var validation = await ValidatorExtensions.ValidateAsync(dto);
            if (validation != null && !validation.IsValid)
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001),
                    validation.ToProblemDetails().Errors);
            }

            // Get user by email
            var userResult = await _userService.GetByEmailAsync(email);

            if (userResult.ResultCode != ResultCodeConst.SYS_Success0002 || userResult.Data is not UserDto userDto)
            {
                _logger.LogWarning("Verify OTP attempt for non-existent email: {Email}", email);
                return new ServiceResult(ResultCodeConst.SYS_Warning0002,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));
            }

            // Find most recent OtpRequest matching userId + type + not used
            var otpRequest = (await _unitOfWork.Repository<OtpRequest, Guid>()
                .GetAllAsync())
                .Where(o => o.UserId == userDto.Id
                    && o.Type == type
                    && !o.IsUsed)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefault();

            if (otpRequest == null)
            {
                _logger.LogWarning("Verify OTP: no OTP request found for {Email} ({Type})", email, type);
                return new ServiceResult(ResultCodeConst.Auth_Warning0017,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0017));
            }

            // Check expiration
            if (otpRequest.ExpiredAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Verify OTP: expired OTP for {Email} ({Type})", email, type);
                return new ServiceResult(ResultCodeConst.Auth_Warning0014,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0014));
            }

            // Check attempt limit
            if (otpRequest.AttemptCount >= OtpConstants.MaxAttempts)
            {
                _logger.LogWarning("Verify OTP: locked for {Email} ({Type})", email, type);
                return new ServiceResult(ResultCodeConst.Auth_Warning0015,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0015));
            }

            // Compare OTP code
            if (otpRequest.Code != otp)
            {
                // Increment attempt count
                otpRequest.AttemptCount++;
                otpRequest.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Repository<OtpRequest, Guid>().UpdateAsync(otpRequest);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogWarning("Verify OTP: invalid OTP for {Email} ({Type}) attempt {Attempt}/{Max}",
                    email, type, otpRequest.AttemptCount, OtpConstants.MaxAttempts);
                return new ServiceResult(ResultCodeConst.Auth_Warning0005,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0005));
            }

            // OTP is valid!
            if (type == OtpType.SignIn)
            {
                // For SignIn: mark as used immediately
                otpRequest.IsUsed = true;
                otpRequest.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Repository<OtpRequest, Guid>().UpdateAsync(otpRequest);
                await _unitOfWork.SaveChangesAsync();
            }
            // For ResetPassword: do NOT mark as used yet (will be used by ResetPasswordAsync)

            _logger.LogInformation("OTP verified successfully for {Email} ({Type})", email, type);
            return new ServiceResult(ResultCodeConst.Auth_Success0010,
                await _msgService.GetMessageAsync(ResultCodeConst.Auth_Success0010),
                new { OtpVerified = true });
        }

        #endregion

        #region Sign-Out

        public async Task<IServiceResult> SignOutAsync(Guid userId, string accessToken)
        {
            try
            {
                // ── Step 1: Blacklist the access token by its JTI ────────────────────────
                // JWT is stateless — even after deleting the refresh token the access token
                // remains cryptographically valid until expiry. We must explicitly revoke it.
                var jwtHandler = new JwtSecurityTokenHandler();
                if (jwtHandler.CanReadToken(accessToken))
                {
                    var parsed = jwtHandler.ReadJwtToken(accessToken);
                    var jti = parsed.Claims
                        .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

                    _logger.LogDebug(
                        "[SignOut] UserId: {UserId} | JTI: {Jti} | TokenExpiry: {Expiry:u}",
                        userId, jti, parsed.ValidTo);

                    if (!string.IsNullOrEmpty(jti))
                    {
                        _tokenBlacklistService.Revoke(jti, parsed.ValidTo);
                        _logger.LogDebug(
                            "[SignOut] Access token blacklisted — JTI: {Jti} (any further API calls with this token will be rejected).",
                            jti);
                    }
                    else
                    {
                        _logger.LogWarning("[SignOut] UserId: {UserId} — access token has no JTI claim; blacklisting skipped.", userId);
                    }
                }
                else
                {
                    _logger.LogWarning("[SignOut] UserId: {UserId} — could not parse access token; blacklisting skipped.", userId);
                }

                // ── Step 2: Delete refresh token to invalidate the refresh flow ──────────
                var getTokenResult = await _refreshTokenService.GetByUserIdAsync(userId);
                if (getTokenResult.Data is not RefreshTokenDto refreshTokenDto)
                {
                    // Already signed out or no refresh token on record (e.g. test env)
                    _logger.LogDebug(
                        "[SignOut] UserId: {UserId} — no active refresh token found (already signed out or session already expired).",
                        userId);
                    return new ServiceResult(
                        ResultCodeConst.Auth_Success0009,
                        await _msgService.GetMessageAsync(ResultCodeConst.Auth_Success0009));
                }

                var deleteResult = await _refreshTokenService.DeleteAsync(refreshTokenDto.Id);
                if (deleteResult.ResultCode == ResultCodeConst.SYS_Success0004)
                {
                    _logger.LogInformation(
                        "[SignOut] UserId: {UserId} signed out — access token blacklisted, refresh token deleted.",
                        userId);
                    return new ServiceResult(
                        ResultCodeConst.Auth_Success0009,
                        await _msgService.GetMessageAsync(ResultCodeConst.Auth_Success0009));
                }

                _logger.LogWarning(
                    "[SignOut] UserId: {UserId} — failed to delete refresh token (id: {TokenId}).",
                    userId, refreshTokenDto.Id);
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SignOut] Unexpected error for UserId: {UserId}.", userId);
                throw;
            }
        }

        #endregion
    }
}