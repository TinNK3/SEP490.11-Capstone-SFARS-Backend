using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
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
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly TokenValidationParameters _tokenValidationParameters;
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
        _emailServiceMock = new Mock<IEmailService>();
        _tokenValidationParameters = new TokenValidationParameters();

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
            _tokenValidationParameters,
            _unitOfWorkMock.Object,
            _jwtUtilsMock.Object,
            _webTokenSettingsMock.Object,
            _loggerMock.Object,
            _externalAuthServiceMock.Object,
            _emailServiceMock.Object
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

    #region ForgotPasswordAsync Tests

    [Fact]
    public async Task ForgotPasswordAsync_EmptyEmail_ReturnsWarning()
    {
        // Arrange
        var email = "";

        // Act
        var result = await _sut.ForgotPasswordAsync(email);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
    }

    [Fact]
    public async Task ForgotPasswordAsync_UserNotFound_ReturnsWarning()
    {
        // Arrange - Trả về warning khi email không tồn tại
        var email = "notfound@test.com";
        
        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0002, "Not found", null!));

        // Act
        var result = await _sut.ForgotPasswordAsync(email);

        // Assert - Trả về warning khi email không tồn tại
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
    }

    [Fact]
    public async Task ForgotPasswordAsync_UserInactive_ReturnsWarning()
    {
        // Arrange
        var email = "inactive@test.com";
        var userDto = CreateValidUserDto();
        userDto.Email = email;
        userDto.Status = UserStatus.Inactive;

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        // Act
        var result = await _sut.ForgotPasswordAsync(email);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0001);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ValidEmail_SendsOtpAndReturnsSuccess()
    {
        // Arrange
        var email = "test@example.com";
        var userDto = CreateValidUserDto();
        userDto.Email = email;

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.Is<EmailMessageDto>(m => m.To == email), It.IsAny<bool>()))
            .ReturnsAsync(true);

        _userServiceMock.Setup(x => x.UpdateEmailVerificationCodeAsync(userDto.Id, It.IsAny<string>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0003, null!, true));

        // Act
        var result = await _sut.ForgotPasswordAsync(email);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0005);
        _emailServiceMock.Verify(x => x.SendEmailAsync(It.Is<EmailMessageDto>(m => m.To == email), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_EmailSendFails_ReturnsFailure()
    {
        // Arrange
        var email = "test@example.com";
        var userDto = CreateValidUserDto();
        userDto.Email = email;

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        // Phải setup UpdateEmailVerificationCodeAsync trả về success trước khi gửi email
        _userServiceMock.Setup(x => x.UpdateEmailVerificationCodeAsync(userDto.Id, It.IsAny<string>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0003, null!, true));

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.Is<EmailMessageDto>(m => m.To == email), It.IsAny<bool>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ForgotPasswordAsync(email);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Fail0002);
    }

    #endregion

    #region ResetPasswordAsync Tests

    [Fact]
    public async Task ResetPasswordAsync_EmptyInputs_ReturnsWarning()
    {
        // Arrange
        var email = "";
        var otp = "";
        var newPassword = "";

        // Act
        var result = await _sut.ResetPasswordAsync(email, otp, newPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
    }

    [Fact]
    public async Task ResetPasswordAsync_UserNotFound_ReturnsWarning()
    {
        // Arrange
        var email = "notfound@test.com";
        var otp = "123456";
        var newPassword = "NewPassword123!";

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0002, "Not found", null!));

        // Act
        var result = await _sut.ResetPasswordAsync(email, otp, newPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidOtp_ReturnsWarning()
    {
        // Arrange
        var email = "test@example.com";
        var otp = "wrong-otp";
        var newPassword = "NewPassword123!";
        
        var userDto = CreateValidUserDto();
        userDto.Email = email;
        userDto.EmailVerificationCode = "correct-otp";

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        // Act
        var result = await _sut.ResetPasswordAsync(email, otp, newPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0005);
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidOtp_UpdatesPassword()
    {
        // Arrange
        var email = "test@example.com";
        var otp = "123456";
        var newPassword = "NewPassword123!";
        
        var userDto = CreateValidUserDto();
        userDto.Email = email;
        userDto.EmailVerificationCode = otp;

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        _userServiceMock.Setup(x => x.UpdatePasswordAsync(userDto.Id, It.IsAny<string>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0003, null!, true));

        // Act
        var result = await _sut.ResetPasswordAsync(email, otp, newPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
        _userServiceMock.Verify(x => x.UpdatePasswordAsync(userDto.Id, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_GoogleUser_CanSetPassword()
    {
        // Arrange - User đăng ký qua Google (không có password), muốn đặt password mới
        var email = "googleuser@test.com";
        var otp = "123456";
        var newPassword = "MyFirstPassword123!";
        
        var userDto = CreateValidUserDto();
        userDto.Email = email;
        userDto.PasswordHash = null; // Google user không có password
        userDto.EmailVerificationCode = otp;

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        _userServiceMock.Setup(x => x.UpdatePasswordAsync(userDto.Id, It.IsAny<string>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0003, null!, true));

        // Act
        var result = await _sut.ResetPasswordAsync(email, otp, newPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
        _userServiceMock.Verify(x => x.UpdatePasswordAsync(userDto.Id, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_UpdateFails_ReturnsFailure()
    {
        // Arrange
        var email = "test@example.com";
        var otp = "123456";
        var newPassword = "NewPassword123!";
        
        var userDto = CreateValidUserDto();
        userDto.Email = email;
        userDto.EmailVerificationCode = otp;

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        _userServiceMock.Setup(x => x.UpdatePasswordAsync(userDto.Id, It.IsAny<string>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Fail0001, null!, false));

        // Act
        var result = await _sut.ResetPasswordAsync(email, otp, newPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Fail0001);
    }

    #endregion

}