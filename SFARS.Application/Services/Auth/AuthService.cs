using Microsoft.AspNetCore.Http.HttpResults;
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
            IEmailService emailService)
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
        }

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
                    //
                    var otpCode = StringUtils.GenerateUniqueCode();

                    //Email Subject and Body
                    var emailsubject = "Your One-Time Password (OTP) for SFARS Sign-In";
                    var emailBody = $@"
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
                                    <p>Mã có hiệu lực trong thời gian ngắn. Vui lòng không chia sẻ mã này.</p>
                                    <p style='margin-top:24px;'>Cảm ơn bạn đã sử dụng SFARS.</p>
                                </div>
                            </div>
                        </div>";

                    // Send OTP email and save to user
                    var isOtpSent = await SendAndSaveOtpAsync(otpCode, authUser, emailsubject, emailBody);
                    if (isOtpSent)
                    {
                        return new ServiceResult(ResultCodeConst.Auth_Success0005,
                            await _msgService.GetMessageAsync(ResultCodeConst.Auth_Success0005),
                            new SignInMethodDto { Method = "otp" });
                    }
                    else //Failed to send OTP email
                    {
                        return new ServiceResult(ResultCodeConst.Auth_Fail0002,
                            await _msgService.GetMessageAsync(ResultCodeConst.Auth_Fail0002));
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
            // Get user by email
            var userResult = await _userService.GetByEmailAsync(user.Email);

            // Handle User authentication
            if (userResult.ResultCode == ResultCodeConst.SYS_Success0002
                && userResult.Data is UserDto userDto)
            {
                // Check match confirmation code 
                if (userDto.EmailVerificationCode == otp)
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
                }
                else
                {
                    return new ServiceResult(ResultCodeConst.Auth_Warning0005,
                            await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0005));
                }
            }
            else
            {
                var message = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002);
                return new ServiceResult(ResultCodeConst.SYS_Warning0002,
                    StringUtils.Format(message, "email"));
            }
            return await AuthenticateUserAsync(user);
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

        public async Task<IServiceResult> ForgotPasswordAsync(string email)
        {
            // Validate using DTO
            var dto = new ForgotPasswordDto { Email = email };
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
                // User not found - return error
                _logger.LogWarning("Forgot password request for non-existent email: {Email}", email);
                return new ServiceResult(ResultCodeConst.SYS_Warning0002,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));
            }

            // Check if user is active
            if (userDto.Status != UserStatus.Active)
            {
                _logger.LogWarning("Forgot password request for inactive user: {Email}", email);
                return new ServiceResult(ResultCodeConst.Auth_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0001));
            }

            // Generate OTP code
            var otpCode = StringUtils.GenerateUniqueCode();

            // Map to AuthUserDto
            var authUser = userDto.ToAuthUserDto();

            // Email Subject and Body
            var emailSubject = "Password Reset OTP for SFARS";
            var emailBody = $@"
                <div style='font-family: Arial, sans-serif; background:#f6f7fb; padding:24px;'>
                    <div style='max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;overflow:hidden;'>
                        <div style='background:#2C3E50;color:#fff;padding:16px 24px;'>
                            <h2 style='margin:0;font-size:20px;'>SFARS Password Reset</h2>
                        </div>
                        <div style='padding:24px;color:#333;line-height:1.6;'>
                            <p>Xin chào <strong>{authUser.FirstName} {authUser.LastName}</strong>,</p>
                            <p>Bạn đã yêu cầu đặt lại mật khẩu. Đây là mã OTP của bạn:</p>
                            <div style='text-align:center;margin:20px 0;'>
                                <span style='display:inline-block;background:#f0f2f7;color:#2C3E50;
                                    font-size:28px;letter-spacing:6px;padding:12px 18px;border-radius:10px;'>
                                    {otpCode}
                                </span>
                            </div>
                            <p>Mã có hiệu lực trong thời gian ngắn. Vui lòng không chia sẻ mã này với bất kỳ ai.</p>
                            <p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                            <p style='margin-top:24px;'>Cảm ơn bạn đã sử dụng SFARS.</p>
                        </div>
                    </div>
                </div>";

            // Send OTP email and save to user
            var isOtpSent = await SendAndSaveOtpAsync(otpCode, authUser, emailSubject, emailBody);

            if (isOtpSent)
            {
                _logger.LogInformation("Password reset OTP sent to {Email}", email);
                return new ServiceResult(ResultCodeConst.Auth_Success0005,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Success0005));
            }

            _logger.LogError("Failed to send password reset OTP to {Email}", email);
            return new ServiceResult(ResultCodeConst.Auth_Fail0002,
                await _msgService.GetMessageAsync(ResultCodeConst.Auth_Fail0002));
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

            // Verify OTP
            if (userDto.EmailVerificationCode != otp)
            {
                _logger.LogWarning("Reset password attempt with invalid OTP for {Email}", email);
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

            // Update user password and clear OTP
            var updateResult = await _userService.UpdatePasswordAsync(userDto.Id, newPasswordHash);

            if (updateResult.ResultCode == ResultCodeConst.SYS_Success0003)
            {
                _logger.LogInformation("Password reset successful for {Email}", email);
                return new ServiceResult(ResultCodeConst.SYS_Success0003,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0003));
            }

            _logger.LogError("Failed to reset password for {Email}", email);
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

        //Send OTP Email
        public async Task<bool> SendAndSaveOtpAsync(string otpCode, AuthUserDto user,
            string subject, string emailBody)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogWarning("Cannot send OTP: missing user email.");
                return false;
            }

            // Save OTP to user
            user.EmailVerificationCode = otpCode;
            var updateResult = await _userService.UpdateEmailVerificationCodeAsync(user.Id, otpCode);

            if (updateResult.Data is not true)
            {
                _logger.LogWarning("Failed to update OTP for user {UserId}.", user.Id);
                return false;
            }

            // Process send email
            var emailMessageDto = new EmailMessageDto
            {
                // Define Recipients
                To = user.Email,
                // Define Subject
                Subject = subject,
                // Define Body
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
    }
}