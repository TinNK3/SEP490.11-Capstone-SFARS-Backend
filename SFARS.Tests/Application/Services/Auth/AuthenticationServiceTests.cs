using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Configurations;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Dtos.User;
using SFARS.Application.Utils;
using SFARS.Application.Services;
using SFARS.Application.Services.Auth;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Models;

namespace SFARS.Tests.Application.Services.Auth;

public class AuthenticationServiceTests
{
    private readonly Mock<IUserService<UserDto>> _userServiceMock;
    private readonly Mock<ISystemMessageService> _msgServiceMock;
    private readonly Mock<IRefreshTokenService<RefreshTokenDto>> _refreshTokenServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IJwtUtils> _jwtUtilsMock;
    private readonly Mock<IOptionsMonitor<WebTokenSettings>> _webTokenSettingsMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly Mock<IExternalAuthService> _externalAuthServiceMock;
    private readonly AuthService _sut; // System Under Test

    public AuthenticationServiceTests()
    {
        _userServiceMock = new Mock<IUserService<UserDto>>();
        _msgServiceMock = new Mock<ISystemMessageService>();
        _refreshTokenServiceMock = new Mock<IRefreshTokenService<RefreshTokenDto>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _jwtUtilsMock = new Mock<IJwtUtils>();
        _webTokenSettingsMock = new Mock<IOptionsMonitor<WebTokenSettings>>();
        _loggerMock = new Mock<ILogger<AuthService>>();
        _externalAuthServiceMock = new Mock<IExternalAuthService>();

        // Setup WebTokenSettings
        var webTokenSettings = new WebTokenSettings
        {
            IssuerSigningKey = "SuperSecretKeyForTestingPurposesOnly12345678",
            ValidIssuer = "SFARS.API.Test",
            ValidAudience = "SFARS.Client.Test",
            TokenLifeTimeInMinutes = 60,
            RefreshTokenLifeTimeInMinutes = 10080
        };
        _webTokenSettingsMock.Setup(x => x.CurrentValue).Returns(webTokenSettings);

        // Setup default message service
        _msgServiceMock.Setup(x => x.GetMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((string msgId) => $"Message for {msgId}");

        // Setup default JwtUtils
        _jwtUtilsMock.Setup(x => x.GenerateJwtTokenAsync(It.IsAny<string>(), It.IsAny<AuthUserDto>()))
            .ReturnsAsync(("test-access-token", DateTime.UtcNow.AddHours(1)));
        _jwtUtilsMock.Setup(x => x.GenerateRefreshTokenAsync())
            .ReturnsAsync("test-refresh-token");

        _sut = new AuthService(
            _userServiceMock.Object,
            _msgServiceMock.Object,
            _refreshTokenServiceMock.Object,
            _unitOfWorkMock.Object,
            _jwtUtilsMock.Object,
            _webTokenSettingsMock.Object,
            _loggerMock.Object,
            _externalAuthServiceMock.Object
        );
    }

    #region SignInWithPasswordAsync Tests

    [Fact]
    public async Task SignInWithPasswordAsync_UserNotFound_ReturnsWarning()
    {
        // Arrange
        var request = new AuthUserDto { Email = "notfound@test.com", Password = "Test123!" };
        
        _userServiceMock.Setup(x => x.GetByEmailAsync(request.Email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0002, "Not found", null!));

        // Act
        var result = await _sut.SignInWithPasswordAsync(request);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task SignInWithPasswordAsync_InvalidPassword_ReturnsWarning()
    {
        // Arrange
        var request = new AuthUserDto { Email = "test@example.com", Password = "WrongPassword" };
        
        var userDto = CreateValidUserDto();
        userDto.PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword("CorrectPassword");

        _userServiceMock.Setup(x => x.GetByEmailAsync(request.Email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        // Act
        var result = await _sut.SignInWithPasswordAsync(request);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0007);
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task SignInWithPasswordAsync_UserInactive_ReturnsWarning()
    {
        // Arrange
        var request = new AuthUserDto { Email = "test@example.com", Password = "Test123!" };
        
        var userDto = CreateValidUserDto();
        userDto.PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword("Test123!");
        userDto.Status = UserStatus.Inactive;

        _userServiceMock.Setup(x => x.GetByEmailAsync(request.Email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        // Act
        var result = await _sut.SignInWithPasswordAsync(request);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0001);
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task SignInWithPasswordAsync_MfaEnabled_ReturnsMfaRequired()
    {
        // Arrange
        var request = new AuthUserDto { Email = "test@example.com", Password = "Test123!" };
        
        var userDto = CreateValidUserDto();
        userDto.PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword("Test123!");
        userDto.TwoFactorEnabled = true;

        _userServiceMock.Setup(x => x.GetByEmailAsync(request.Email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        // Act
        var result = await _sut.SignInWithPasswordAsync(request);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0010);
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task SignInWithPasswordAsync_ValidCredentials_ReturnsTokens()
    {
        // Arrange
        var request = new AuthUserDto { Email = "test@example.com", Password = "Test123!" };
        
        var userDto = CreateValidUserDto();
        userDto.PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword("Test123!");

        _userServiceMock.Setup(x => x.GetByEmailAsync(request.Email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        _refreshTokenServiceMock.Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, null!));

        _refreshTokenServiceMock.Setup(x => x.CreateAsync(It.IsAny<RefreshTokenDto>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0001, null!, null!));

        // Act
        var result = await _sut.SignInWithPasswordAsync(request);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0002);
        result.Data.Should().NotBeNull();
        result.Data.Should().BeOfType<AuthResultDto>();
        
        var authResult = result.Data as AuthResultDto;
        authResult!.AccessToken.Should().NotBeNullOrEmpty();
        authResult.RefreshToken.Should().NotBeNullOrEmpty();
        authResult.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task SignInWithPasswordAsync_ExistingRefreshToken_UpdatesToken()
    {
        // Arrange
        var request = new AuthUserDto { Email = "test@example.com", Password = "Test123!" };
        var userId = Guid.NewGuid();
        
        var userDto = CreateValidUserDto();
        userDto.Id = userId;
        userDto.PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword("Test123!");

        var existingRefreshToken = new RefreshTokenDto
        {
            Id = 1,
            UserId = userId,
            RefreshTokenId = "old-token",
            TokenId = "old-token-id"
        };

        _userServiceMock.Setup(x => x.GetByEmailAsync(request.Email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        _refreshTokenServiceMock.Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, existingRefreshToken));

        _refreshTokenServiceMock.Setup(x => x.UpdateAsync(It.IsAny<int>(), It.IsAny<RefreshTokenDto>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0003, null!, null!));

        // Act
        var result = await _sut.SignInWithPasswordAsync(request);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0002);
        _refreshTokenServiceMock.Verify(x => x.UpdateAsync(It.IsAny<int>(), It.IsAny<RefreshTokenDto>()), Times.Once);
    }

    #endregion

    #region Helper Methods

    private UserDto CreateValidUserDto()
    {
        return new UserDto
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Status = UserStatus.Active,
            TwoFactorEnabled = false,
            Role = "User"
        };
        #endregion

    }

    #region SignInWithGoogleAsync Tests

    [Fact]
    public async Task SignInWithGoogleAsync_ValidToken_ExistingUser_ReturnsTokens()
    {
        // Arrange
        var token = "valid-google-token-which-is-long-enough";
        var externalUser = new ExternalAuthUser
        {
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Avatar = "http://avatar.url",
            ProviderId = "google-123"
        };

        _externalAuthServiceMock.Setup(x => x.VerifyGoogleTokenAsync(token))
            .ReturnsAsync(externalUser);

        var userDto = CreateValidUserDto();
        _userServiceMock.Setup(x => x.GetByEmailAsync(externalUser.Email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        _refreshTokenServiceMock.Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0002, "Not found", null!));

        _refreshTokenServiceMock.Setup(x => x.CreateAsync(It.IsAny<RefreshTokenDto>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0001, null!, new RefreshTokenDto
            {
                Id = 1,
                UserId = userDto.Id,
                RefreshTokenId = "test-refresh-token",
                TokenId = "token-id",
                CreateDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(7)
            }));

        // Act
        var result = await _sut.SignInWithGoogleAsync(token);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0002);
        result.Data.Should().BeOfType<AuthResultDto>();
    }

    [Fact]
    public async Task SignInWithGoogleAsync_InvalidToken_ThrowsUnauthorized()
    {
        // Arrange (token đủ dài để qua validator)
        var token = "invalid-token-which-is-long-enough";
        _externalAuthServiceMock.Setup(x => x.VerifyGoogleTokenAsync(token))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid Google Token."));

        // Act
        Func<Task> act = async () => await _sut.SignInWithGoogleAsync(token);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid Google Token.");
    }

    #endregion

}