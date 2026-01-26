using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFARS.Application.Common;
using SFARS.Application.Configurations;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Dtos.User;
using SFARS.Application.Exceptions;
using SFARS.Application.Utils;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Application.Services.Auth
{
    public class AuthenticationService : IAuthenticationService<AuthenticateUserDto>
    {
        private readonly IUserService<UserDto> _userService;
        private readonly ISystemMessageService _msgService;
        private readonly IRefreshTokenService<RefreshTokenDto> _refreshTokenService;
        private readonly WebTokenSettings _webTokenSettings;
        private readonly ILogger<AuthenticationService> _logger;

        public AuthenticationService(
            IUserService<UserDto> userService,
            ISystemMessageService msgService,
            IRefreshTokenService<RefreshTokenDto> refreshTokenService,
            IOptionsMonitor<WebTokenSettings> monitor,
            ILogger<AuthenticationService> logger)
        {
            _userService = userService;
            _msgService = msgService;
            _refreshTokenService = refreshTokenService;
            _webTokenSettings = monitor.CurrentValue;
            _logger = logger;
        }

        public async Task<IServiceResult> SignInWithPasswordAsync(AuthenticateUserDto user)
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
                    // Password not match
                    return new ServiceResult(ResultCodeConst.Auth_Warning0007,
                        await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0007));
                }

                // Check if MFA is enabled
                if (userDto.TwoFactorEnabled)
                {
                    return new ServiceResult(ResultCodeConst.Auth_Warning0010,
                        await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0010));
                }

                // Check if user is active
                if (userDto.Status != UserStatus.Active)
                {
                    return new ServiceResult(ResultCodeConst.Auth_Warning0001,
                        await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0001));
                }

                // Map user info to authenticate user
                var authenticateUser = new AuthenticateUserDto
                {
                    Id = userDto.Id,
                    Email = userDto.Email,
                    FirstName = userDto.FirstName,
                    LastName = userDto.LastName,
                    Phone = userDto.Phone,
                    Avatar = userDto.Avatar,
                    Address = userDto.Address,
                    Gender = userDto.Gender?.ToString(),
                    Dob = userDto.Dob,
                    IsActive = userDto.Status == UserStatus.Active,
                    CreateDate = userDto.CreateDate,
                    ModifiedDate = userDto.ModifiedDate,
                    RoleName = userDto.Role?.RoleName ?? "User",
                    IsRescuer = userDto.Role?.RoleName == "Rescuer"
                };

                // Handle authenticate user and generate tokens
                return await AuthenticateUserAsync(authenticateUser);
            }
            else
            {
                // User not found
                var message = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002);
                return new ServiceResult(ResultCodeConst.SYS_Warning0002,
                    StringUtils.Format(message, "email"));
            }
        }

        public Task<IServiceResult> SignUpAsync(AuthenticateUserDto user)
        {
            // TODO: Implement sign up logic
            throw new NotImplementedException("SignUp is not implemented yet");
        }

        // Handle refresh token
        private async Task<RefreshTokenDto> HandleRefreshTokenAsync(AuthenticateUserDto user, string tokenId)
        {
            var getTokenResult = await _refreshTokenService.GetByUserIdAsync(user.Id);

            if (getTokenResult.Data is null)
            {
                return await CreateNewRefreshTokenAsync(user, tokenId);
            }

            return await UpdateExistingRefreshTokenAsync(getTokenResult.Data, tokenId);
        }

        // Create new refresh token 
        private async Task<RefreshTokenDto> CreateNewRefreshTokenAsync(AuthenticateUserDto user, string tokenId)
        {
            var refreshTokenId = await new JwtUtils().GenerateRefreshTokenAsync();

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
                throw new Exception(StringUtils.Format(errMsg, "refresh token"));
            }

            return refreshTokenDto;
        }

        // Update existing refresh token
        private async Task<RefreshTokenDto> UpdateExistingRefreshTokenAsync(RefreshTokenDto refreshTokenDto, string tokenId)
        {
            refreshTokenDto.CreateDate = DateTime.UtcNow;
            refreshTokenDto.RefreshTokenId = await new JwtUtils().GenerateRefreshTokenAsync();
            refreshTokenDto.TokenId = tokenId;
            refreshTokenDto.RefreshCount = 0;

            var result = await _refreshTokenService.UpdateAsync(refreshTokenDto.Id, refreshTokenDto);
            if (result.ResultCode != ResultCodeConst.SYS_Success0003)
            {
                throw new Exception(await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0003));
            }

            return refreshTokenDto;
        }

        // Validate password
        private bool ValidatePassword(string? inputPassword, string? storedHash)
        {
            if (string.IsNullOrEmpty(storedHash))
            {
                throw new UnauthorizedException("Your password is not set. Please sign in with an external provider to update it.");
            }

            return HashUtils.VerifyPassword(inputPassword ?? string.Empty, storedHash);
        }

        private async Task<ServiceResult> AuthenticateUserAsync(AuthenticateUserDto? user)
        {
            // Check not exist authenticate user (save fail,...) 
            if (user == null)
                throw new Exception("Unknown error invoke while authenticating user.");

            // Validate user status
            if (user.Id == Guid.Empty || string.IsNullOrEmpty(user.RoleName))
            {
                return new ServiceResult(ResultCodeConst.Auth_Warning0007,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0007));
            }

            if (!user.IsActive)
            {
                return new ServiceResult(ResultCodeConst.Auth_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0001));
            }

            // Generate token
            var tokenId = Guid.NewGuid().ToString();
            var jwtResponse = await new JwtUtils(_webTokenSettings).GenerateJwtTokenAsync(
                tokenId: tokenId, user: user);

            if (string.IsNullOrEmpty(jwtResponse.AccessToken) || jwtResponse.ValidTo <= DateTime.UtcNow)
                throw new Exception("Invalid JWT token generated");

            // Handle refresh token
            var refreshTokenDto = await HandleRefreshTokenAsync(user, tokenId);

            return new ServiceResult(
                ResultCodeConst.Auth_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.Auth_Success0002),
                new AuthenticateResultDto
                {
                    AccessToken = jwtResponse.AccessToken,
                    RefreshToken = refreshTokenDto.RefreshTokenId,
                    ValidTo = jwtResponse.ValidTo
                });
        }
    }
}
