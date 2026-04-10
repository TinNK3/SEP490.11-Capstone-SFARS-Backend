using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Hangfire;
using SFARS.Application.Common;
using SFARS.Application.Configurations;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Dtos.User;
using SFARS.Application.Utils;
using SFARS.Application.Services;
using SFARS.Application.Services.Auth;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

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
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
    private readonly Mock<ITokenBlacklistService> _tokenBlacklistServiceMock;
    private readonly Mock<IGenericRepository<OtpRequest, Guid>> _otpRepoMock;
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
        _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        _tokenBlacklistServiceMock = new Mock<ITokenBlacklistService>();
        _otpRepoMock = new Mock<IGenericRepository<OtpRequest, Guid>>();
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

        // Setup UnitOfWork to return the OtpRequest repository mock
        _unitOfWorkMock.Setup(x => x.Repository<OtpRequest, Guid>())
            .Returns(_otpRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<UserLoginHistory, long>()).Returns(new Moq.Mock<IGenericRepository<UserLoginHistory, long>>().Object);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

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
            _emailServiceMock.Object,
            _backgroundJobClientMock.Object,
            _tokenBlacklistServiceMock.Object,
            new Moq.Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object
        );
    }

    #region SignInWithPasswordAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SignInWithPasswordAsync when user is not found
    /// Precondition: Email does not exist in the database
    /// Expected Result: Returns warning SYS_Warning0002 with empty Data
    /// </summary>
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

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SignInWithPasswordAsync with invalid password
    /// Precondition: Valid email but incorrect password
    /// Expected Result: Returns warning Auth_Warning0007 with empty Data
    /// </summary>
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

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SignInWithPasswordAsync when user status is inactive
    /// Precondition: User credentials are correct but user account is disabled/inactive
    /// Expected Result: Returns warning Auth_Warning0001 with empty Data
    /// </summary>
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

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: SignInWithPasswordAsync when MFA is enabled
    /// Precondition: Correct credentials and TwoFactorEnabled is true
    /// Expected Result: Returns warning Auth_Warning0010 indicating MFA required
    /// </summary>
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

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SignInWithPasswordAsync with fully valid credentials and active account
    /// Precondition: Correct email and password, account active, no MFA
    /// Expected Result: Returns success Auth_Success0002 with AuthResultDto containing JWT tokens
    /// </summary>
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

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SignInWithPasswordAsync when user has an existing refresh token
    /// Precondition: Valid credentials and user already has an active refresh token
    /// Expected Result: Returns success Auth_Success0002 and updates existing refresh token
    /// </summary>
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

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SignInWithGoogleAsync with valid token and existing user
    /// Precondition: Google token is valid and user exists in system
    /// Expected Result: Returns success Auth_Success0002 with AuthResultDto containing tokens
    /// </summary>
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

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SignInWithGoogleAsync with invalid token
    /// Precondition: Google token is invalid or expired
    /// Expected Result: Throws UnauthorizedAccessException
    /// </summary>
    [Fact]
    public async Task SignInWithGoogleAsync_InvalidToken_ThrowsUnauthorized()
    {
        // Arrange (token length is sufficient to pass validation)
        var token = "invalid-token-which-is-long-enough";
        _externalAuthServiceMock.Setup(x => x.VerifyGoogleTokenAsync(token))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid Google Token."));

        // Act
        Func<Task> act = async () => await _sut.SignInWithGoogleAsync(token);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid Google Token.");
    }

    [Fact]
    public async Task SignInWithGoogleAsync_AdminLogin_NonAdminUser_ReturnsForbiddenWarning()
    {
        // Arrange
        var token = "valid-google-token-which-is-long-enough";
        var externalUser = new ExternalAuthUser
        {
            Email = "user@example.com",
            FirstName = "Normal",
            LastName = "User",
            ProviderId = "google-456"
        };

        _externalAuthServiceMock.Setup(x => x.VerifyGoogleTokenAsync(token))
            .ReturnsAsync(externalUser);

        var userDto = CreateValidUserDto();
        userDto.Email = externalUser.Email;
        userDto.Role = UserTypeConstants.User;

        _userServiceMock.Setup(x => x.GetByEmailAsync(externalUser.Email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        // Act
        var result = await _sut.SignInWithGoogleAsync(token, isAdminLogin: true);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0007);
    }

    [Fact]
    public async Task SignInWithGoogleAsync_AdminLogin_AdminUser_ReturnsTokens()
    {
        // Arrange
        var token = "valid-google-token-which-is-long-enough";
        var externalUser = new ExternalAuthUser
        {
            Email = "admin@example.com",
            FirstName = "Admin",
            LastName = "User",
            ProviderId = "google-admin-123"
        };

        _externalAuthServiceMock.Setup(x => x.VerifyGoogleTokenAsync(token))
            .ReturnsAsync(externalUser);

        var adminUserDto = CreateValidUserDto();
        adminUserDto.Email = externalUser.Email;
        adminUserDto.Role = UserTypeConstants.Admin;

        _userServiceMock.Setup(x => x.GetByEmailAsync(externalUser.Email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, adminUserDto));

        _refreshTokenServiceMock.Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0002, "Not found", null!));

        _refreshTokenServiceMock.Setup(x => x.CreateAsync(It.IsAny<RefreshTokenDto>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0001, null!, new RefreshTokenDto
            {
                Id = 1,
                UserId = adminUserDto.Id,
                RefreshTokenId = "test-refresh-token",
                TokenId = "token-id",
                CreateDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(7)
            }));

        // Act
        var result = await _sut.SignInWithGoogleAsync(token, isAdminLogin: true);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0002);
        result.Data.Should().BeOfType<AuthResultDto>();
    }

    [Fact]
    public async Task SignInWithGoogleAsync_AdminLogin_UserNotFound_ReturnsForbiddenWarning()
    {
        // Arrange
        var token = "valid-google-token-which-is-long-enough";
        var externalUser = new ExternalAuthUser
        {
            Email = "missing-admin@example.com",
            FirstName = "Missing",
            LastName = "Admin",
            ProviderId = "google-missing-1"
        };

        _externalAuthServiceMock.Setup(x => x.VerifyGoogleTokenAsync(token))
            .ReturnsAsync(externalUser);

        _userServiceMock.Setup(x => x.GetByEmailAsync(externalUser.Email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0002, "Not found", null!));

        // Act
        var result = await _sut.SignInWithGoogleAsync(token, isAdminLogin: true);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0007);
    }

    #endregion

    #region SignInAsync (Check Login Method) Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SignInAsync when user with provided email does not exist
    /// Precondition: Email not found in database via account lookup
    /// Expected Result: Returns warning SYS_Warning0002
    /// </summary>
    [Fact]
    public async Task SignInAsync_UserNotFound_ReturnsNotFoundWarning()
    {
        // Arrange
        var email = "nonexistent@test.com";
        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0004, "Not found", null!));

        // Act
        var result = await _sut.SignInAsync(email);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SignInAsync when user is inactive or banned
    /// Precondition: User exists but status is set to Inactive
    /// Expected Result: Returns warning Auth_Warning0001
    /// </summary>
    [Fact]
    public async Task SignInAsync_UserInactive_ReturnsInactiveWarning()
    {
        // Arrange
        var email = "inactive@test.com";
        var userDto = CreateValidUserDto();
        userDto.Status = UserStatus.Inactive;

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        // Act
        var result = await _sut.SignInAsync(email);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0001);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SignInAsync for a user with a local password
    /// Precondition: User exists and has a PasswordHash set
    /// Expected Result: Returns Auth_Success0001 with method "password"
    /// </summary>
    [Fact]
    public async Task SignInAsync_UserWithPassword_ReturnsPasswordMethod()
    {
        // Arrange
        var email = "password_user@test.com";
        var userDto = CreateValidUserDto();
        userDto.PasswordHash = "some_hashed_password"; // Not empty

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        // Act
        var result = await _sut.SignInAsync(email);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0001);
        result.Data.Should().NotBeNull();
        var data = result.Data.Should().BeOfType<SignInMethodDto>().Subject;
        data.Method.Should().Be("password");

        // Verify NO OTP email was sent
        _emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<EmailMessageDto>(), It.IsAny<bool>()), Times.Never);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SignInAsync for a Google user (no local password)
    /// Precondition: User exists but PasswordHash is empty/null/whitespace
    /// Expected Result: Returns Auth_Success0011 with method "otp"
    /// </summary>
    [Fact]
    public async Task SignInAsync_UserWithoutPassword_ReturnsOtpMethod()
    {
        // Arrange
        var email = "google_user@test.com";
        var userDto = CreateValidUserDto();
        userDto.PasswordHash = ""; // Empty password = Google user

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        // Act
        var result = await _sut.SignInAsync(email);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0011);
        result.Data.Should().NotBeNull();
        var data = result.Data.Should().BeOfType<SignInMethodDto>().Subject;
        data.Method.Should().Be("otp");

        // IMPORTANT VERIFICATION: No OTP email was sent as a side effect
        _emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<EmailMessageDto>(), It.IsAny<bool>()), Times.Never);
    }

    #endregion

    #region ForgotPasswordAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: ForgotPasswordAsync with empty email
    /// Precondition: Email string is empty
    /// Expected Result: Returns warning SYS_Warning0001
    /// </summary>
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

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: ForgotPasswordAsync with valid active user
    /// Precondition: Valid email mapped to active user, email sending succeeds
    /// Expected Result: Returns success Auth_Success0005
    /// </summary>
    [Fact]
    public async Task ForgotPasswordAsync_UserNotFound_ReturnsWarning()
    {
        // Arrange
        var email = "notfound@test.com";
        
        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0002, "Not found", null!));

        // Act
        var result = await _sut.ForgotPasswordAsync(email);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: ForgotPasswordAsync when user account is inactive
    /// Precondition: Provided email maps to inactive user
    /// Expected Result: Returns warning Auth_Warning0001
    /// </summary>
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

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: ForgotPasswordAsync with valid active user
    /// Precondition: Valid email mapped to active user, email sending succeeds
    /// Expected Result: Returns success Auth_Success0005
    /// </summary>
    [Fact]
    public async Task ForgotPasswordAsync_ValidEmail_SendsOtpAndReturnsSuccess()
    {
        // Arrange
        var email = "test@example.com";
        var userDto = CreateValidUserDto();
        userDto.Email = email;

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        _otpRepoMock.Setup(x => x.GetAllAsync(It.IsAny<bool>()))
            .ReturnsAsync(Enumerable.Empty<OtpRequest>());

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.Is<EmailMessageDto>(m => m.To == email), It.IsAny<bool>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ForgotPasswordAsync(email);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0005);
        _emailServiceMock.Verify(x => x.SendEmailAsync(It.Is<EmailMessageDto>(m => m.To == email), It.IsAny<bool>()), Times.Once);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: ForgotPasswordAsync when email sending fails
    /// Precondition: Valid email, but external email service fails to send OTP
    /// Expected Result: Returns failure Auth_Fail0002
    /// </summary>
    [Fact]
    public async Task ForgotPasswordAsync_EmailSendFails_ReturnsFailure()
    {
        // Arrange
        var email = "test@example.com";
        var userDto = CreateValidUserDto();
        userDto.Email = email;

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        _otpRepoMock.Setup(x => x.GetAllAsync(It.IsAny<bool>()))
            .ReturnsAsync(Enumerable.Empty<OtpRequest>());

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.Is<EmailMessageDto>(m => m.To == email), It.IsAny<bool>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ForgotPasswordAsync(email);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Fail0002);
    }

    #endregion

    #region SignOutAsync Tests

    // -----------------------------------------------------------------------------
    // Helper: build a real signed JWT so CanReadToken() + ReadJwtToken() work.
    // SignOutAsync parses the token inline (not via IJwtUtils), so tests must supply
    // an actual JWT string to exercise the blacklisting branch.
    // -----------------------------------------------------------------------------
    private static string BuildRealJwt(
        string? jti = null,
        int expiresInMinutes = 60,
        string signingKey = "SuperSecretKeyForTestingPurposesOnly12345678")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        };

        // Only add JTI claim when explicitly provided (allows testing the no-JTI path)
        if (jti != null)
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));

        var token = new JwtSecurityToken(
            issuer: "SFARS.API.Test",
            audience: "SFARS.Client.Test",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // STEP 1: Blacklisting

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SignOutAsync with valid JWT
    /// Precondition: Valid access token provided
    /// Expected Result: Access token JTI is added to the token blacklist
    /// </summary>
    [Fact]
    public async Task SignOutAsync_WithValidJwt_RevokesJtiOnBlacklist()
    {
        // Arrange - real JWT so CanReadToken() returns true and JTI is extracted
        var userId = Guid.NewGuid();
        var expectedJti = Guid.NewGuid().ToString();
        var accessToken = BuildRealJwt(jti: expectedJti);

        _refreshTokenServiceMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0004, "Not found", null!));

        // Act
        await _sut.SignOutAsync(userId, accessToken);

        // Assert - Revoke must be called with the exact JTI extracted from the token
        _tokenBlacklistServiceMock.Verify(
            x => x.Revoke(expectedJti, It.IsAny<DateTime>()),
            Times.Once);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SignOutAsync parses expiry correctly for blacklist
    /// Precondition: Valid token with specific expiry
    /// Expected Result: Token is revoked with correct expiry matching ValidTo
    /// </summary>
    [Fact]
    public async Task SignOutAsync_WithValidJwt_RevokesWithCorrectExpiry()
    {
        // Arrange - verify the expiry passed to Revoke matches the token's ValidTo
        var userId = Guid.NewGuid();
        var expectedJti = Guid.NewGuid().ToString();
        var accessToken = BuildRealJwt(jti: expectedJti, expiresInMinutes: 30);

        // Parse the token ourselves to know exactly what ValidTo will be
        var parsedValidTo = new JwtSecurityTokenHandler()
            .ReadJwtToken(accessToken).ValidTo;

        _refreshTokenServiceMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0004, "Not found", null!));

        // Act
        await _sut.SignOutAsync(userId, accessToken);

        // Assert - expiry forwarded to blacklist must match the token's actual ValidTo
        _tokenBlacklistServiceMock.Verify(
            x => x.Revoke(expectedJti, It.Is<DateTime>(d =>
                Math.Abs((d - parsedValidTo).TotalSeconds) < 2)),
            Times.Once);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SignOutAsync with unparsable token safely skips
    /// Precondition: Malformed access token provided
    /// Expected Result: Skips blacklisting, doesn't throw exceptions
    /// </summary>
    [Fact]
    public async Task SignOutAsync_WithUnparsableToken_SkipsBlacklisting()
    {
        // Arrange - garbage string, CanReadToken() returns false; blacklist must be skipped
        var userId = Guid.NewGuid();
        const string garbageToken = "not.a.valid.jwt.at.all";

        _refreshTokenServiceMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0004, "Not found", null!));

        // Act
        await _sut.SignOutAsync(userId, garbageToken);

        // Assert - Revoke must never be called
        _tokenBlacklistServiceMock.Verify(
            x => x.Revoke(It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SignOutAsync with token missing JTI claim
    /// Precondition: Token is missing 'jti' claim
    /// Expected Result: Skips blacklisting
    /// </summary>
    [Fact]
    public async Task SignOutAsync_WithJwtMissingJtiClaim_SkipsBlacklisting()
    {
        // Arrange - real JWT but no JTI claim; service should log warning and skip Revoke
        var userId = Guid.NewGuid();
        var accessToken = BuildRealJwt(jti: null); // no JTI

        _refreshTokenServiceMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0004, "Not found", null!));

        // Act
        await _sut.SignOutAsync(userId, accessToken);

        // Assert
        _tokenBlacklistServiceMock.Verify(
            x => x.Revoke(It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Never);
    }

            // STEP 2: Refresh token deletion

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SignOutAsync with active session deletes refresh token
    /// Precondition: Valid JWT and an active refresh token exists
    /// Expected Result: Token is blacklisted and refresh token is deleted successfully
    /// </summary>
    [Fact]
    public async Task SignOutAsync_WithValidJwt_ActiveSession_BlacklistsAndDeletesRefreshToken()
    {
        // Arrange - happy path: valid JWT + active refresh token
        var userId = Guid.NewGuid();
        var jti = Guid.NewGuid().ToString();
        var accessToken = BuildRealJwt(jti: jti);
        var refreshTokenDto = new RefreshTokenDto
        {
            Id = 42,
            UserId = userId,
            RefreshTokenId = "refresh-abc",
            TokenId = jti,
            CreateDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        };

        _refreshTokenServiceMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, refreshTokenDto));
        _refreshTokenServiceMock
            .Setup(x => x.DeleteAsync(refreshTokenDto.Id))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0004, null!, true));

        // Act
        var result = await _sut.SignOutAsync(userId, accessToken);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0009);
        _tokenBlacklistServiceMock.Verify(x => x.Revoke(jti, It.IsAny<DateTime>()), Times.Once);
        _refreshTokenServiceMock.Verify(x => x.DeleteAsync(refreshTokenDto.Id), Times.Once);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SignOutAsync without active session succeeds
    /// Precondition: Valid JWT but no active refresh token in database
    /// Expected Result: Token is blacklisted successfully without deletion errors
    /// </summary>
    [Fact]
    public async Task SignOutAsync_WithValidJwt_NoActiveSession_ReturnsSuccessWithoutDeletion()
    {
        // Arrange - no refresh token on record (already signed out / session expired)
        var userId = Guid.NewGuid();
        var accessToken = BuildRealJwt(jti: Guid.NewGuid().ToString());

        _refreshTokenServiceMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0004, "Not found", null!));

        // Act
        var result = await _sut.SignOutAsync(userId, accessToken);

        // Assert - still succeeds; token is blacklisted to prevent reuse
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0009);
        _refreshTokenServiceMock.Verify(x => x.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SignOutAsync when delete refresh token fails
    /// Precondition: Valid JWT, refresh token exists but database deletion fails
    /// Expected Result: Returns failure SYS_Fail0001
    /// </summary>
    [Fact]
    public async Task SignOutAsync_WithValidJwt_DeleteFails_ReturnsFailure()
    {
        // Arrange - blacklist succeeds but DB delete fails
        var userId = Guid.NewGuid();
        var jti = Guid.NewGuid().ToString();
        var accessToken = BuildRealJwt(jti: jti);
        var refreshTokenDto = new RefreshTokenDto
        {
            Id = 99,
            UserId = userId,
            RefreshTokenId = "refresh-xyz",
            TokenId = jti
        };

        _refreshTokenServiceMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, refreshTokenDto));
        _refreshTokenServiceMock
            .Setup(x => x.DeleteAsync(refreshTokenDto.Id))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Fail0001, "Delete failed", false));

        // Act
        var result = await _sut.SignOutAsync(userId, accessToken);

        // Assert - JTI is still blacklisted even though delete failed
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Fail0001);
        result.Data.Should().BeNull();
        _tokenBlacklistServiceMock.Verify(x => x.Revoke(jti, It.IsAny<DateTime>()), Times.Once);
        _refreshTokenServiceMock.Verify(x => x.DeleteAsync(refreshTokenDto.Id), Times.Once);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SignOutAsync deletes the correct token by ID
    /// Precondition: Specific refresh token ID associated with user
    /// Expected Result: DeleteAsync invoked with correct specific token ID
    /// </summary>
    [Fact]
    public async Task SignOutAsync_DeletesCorrectRefreshTokenId()
    {
        // Arrange - verifies DeleteAsync is called with the exact token ID from the lookup
        var userId = Guid.NewGuid();
        const int expectedTokenId = 77;
        var accessToken = BuildRealJwt(jti: Guid.NewGuid().ToString());
        var refreshTokenDto = new RefreshTokenDto
        {
            Id = expectedTokenId,
            UserId = userId,
            RefreshTokenId = "some-token",
            TokenId = "some-jti"
        };

        _refreshTokenServiceMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, refreshTokenDto));
        _refreshTokenServiceMock
            .Setup(x => x.DeleteAsync(expectedTokenId))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0004, null!, true));

        // Act
        await _sut.SignOutAsync(userId, accessToken);

        // Assert
        _refreshTokenServiceMock.Verify(x => x.DeleteAsync(expectedTokenId), Times.Once);
        _refreshTokenServiceMock.Verify(
            x => x.DeleteAsync(It.Is<int>(id => id != expectedTokenId)), Times.Never);
    }

            // Exception paths

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SignOutAsync when getting user token throws exception
    /// Precondition: Database connection breaks during lookup
    /// Expected Result: Exception is propagated
    /// </summary>
    [Fact]
    public async Task SignOutAsync_GetByUserIdThrowsException_PropagatesException()
    {
        // Arrange - DB failure during refresh token lookup
        var userId = Guid.NewGuid();
        var accessToken = BuildRealJwt(jti: Guid.NewGuid().ToString());

        _refreshTokenServiceMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ThrowsAsync(new InvalidOperationException("Database connection lost"));

        // Act
        Func<Task> act = async () => await _sut.SignOutAsync(userId, accessToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database connection lost");
        _refreshTokenServiceMock.Verify(x => x.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SignOutAsync when token deletion throws exception
    /// Precondition: Database connection breaks during deletion
    /// Expected Result: Exception is propagated
    /// </summary>
    [Fact]
    public async Task SignOutAsync_DeleteThrowsException_PropagatesException()
    {
        // Arrange - delete blows up (e.g. concurrency conflict)
        var userId = Guid.NewGuid();
        var accessToken = BuildRealJwt(jti: Guid.NewGuid().ToString());
        var refreshTokenDto = new RefreshTokenDto
        {
            Id = 5,
            UserId = userId,
            RefreshTokenId = "r-token",
            TokenId = "j-token"
        };

        _refreshTokenServiceMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, refreshTokenDto));
        _refreshTokenServiceMock
            .Setup(x => x.DeleteAsync(refreshTokenDto.Id))
            .ThrowsAsync(new InvalidOperationException("Concurrency error"));

        // Act
        Func<Task> act = async () => await _sut.SignOutAsync(userId, accessToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Concurrency error");
    }

    // UserId routing

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SignOutAsync uses the correct user ID for token lookup
    /// Precondition: Specific user ID provided
    /// Expected Result: Looks up tokens specifically for that user ID
    /// </summary>
    [Fact]
    public async Task SignOutAsync_LooksUpCorrectUserId()
    {
        // Arrange - verify GetByUserIdAsync receives the exact userId passed in
        var userId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var accessToken = BuildRealJwt(jti: Guid.NewGuid().ToString());

        _refreshTokenServiceMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0004, null!, null!));

        // Act
        await _sut.SignOutAsync(userId, accessToken);

        // Assert
        _refreshTokenServiceMock.Verify(x => x.GetByUserIdAsync(userId), Times.Once);
        _refreshTokenServiceMock.Verify(x => x.GetByUserIdAsync(differentUserId), Times.Never);
    }

    #endregion

    #region ResetPasswordAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: ResetPasswordAsync with empty inputs
    /// Precondition: Email, OTP, and new password are empty strings
    /// Expected Result: Returns warning SYS_Warning0001
    /// </summary>
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

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: ResetPasswordAsync when user not found
    /// Precondition: Provided email does not match any user
    /// Expected Result: Returns warning SYS_Warning0002
    /// </summary>
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

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: ResetPasswordAsync with invalid OTP
    /// Precondition: OTP code does not match the stored OTP
    /// Expected Result: Returns warning Auth_Warning0005
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_InvalidOtp_ReturnsWarning()
    {
        // Arrange
        var email = "test@example.com";
        var otp = "654321";
        var newPassword = "NewPassword123!";
        
        var userDto = CreateValidUserDto();
        userDto.Email = email;

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        var existingOtp = new OtpRequest
        {
            Id = Guid.NewGuid(),
            UserId = userDto.Id,
            Code = "123456",
            Type = OtpType.ResetPassword,
            IsUsed = false,
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiredAt = DateTime.UtcNow.AddMinutes(2)
        };

        _otpRepoMock.Setup(x => x.GetAllAsync(It.IsAny<bool>()))
            .ReturnsAsync(new List<OtpRequest> { existingOtp });

        // Act
        var result = await _sut.ResetPasswordAsync(email, otp, newPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0005);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: ResetPasswordAsync with valid OTP
    /// Precondition: Valid email, valid OTP matching database
    /// Expected Result: Returns success SYS_Success0003 and password is updated
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_ValidOtp_UpdatesPassword()
    {
        // Arrange
        var email = "test@example.com";
        var otp = "123456";
        var newPassword = "NewPassword123!";
        
        var userDto = CreateValidUserDto();
        userDto.Email = email;

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        var existingOtp = new OtpRequest
        {
            Id = Guid.NewGuid(),
            UserId = userDto.Id,
            Code = otp,
            Type = OtpType.ResetPassword,
            IsUsed = false,
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiredAt = DateTime.UtcNow.AddMinutes(2)
        };

        _otpRepoMock.Setup(x => x.GetAllAsync(It.IsAny<bool>()))
            .ReturnsAsync(new List<OtpRequest> { existingOtp });

        _userServiceMock.Setup(x => x.UpdatePasswordAsync(userDto.Id, It.IsAny<string>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0003, null!, true));

        // Act
        var result = await _sut.ResetPasswordAsync(email, otp, newPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
        _userServiceMock.Verify(x => x.UpdatePasswordAsync(userDto.Id, It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: ResetPasswordAsync to set initial password for Google Auth user
    /// Precondition: Valid OAuth user without existing password requests reset
    /// Expected Result: Returns success SYS_Success0003 and password is updated
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_GoogleUser_CanSetPassword()
    {
        // Arrange - User registered via Google (no password), wants to set a new password
        var email = "googleuser@test.com";
        var otp = "123456";
        var newPassword = "MyFirstPassword123!";
        
        var userDto = CreateValidUserDto();
        userDto.Email = email;
        userDto.PasswordHash = null; // Google user has no local password yet

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        var existingOtp = new OtpRequest
        {
            Id = Guid.NewGuid(),
            UserId = userDto.Id,
            Code = otp,
            Type = OtpType.ResetPassword,
            IsUsed = false,
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiredAt = DateTime.UtcNow.AddMinutes(2)
        };

        _otpRepoMock.Setup(x => x.GetAllAsync(It.IsAny<bool>()))
            .ReturnsAsync(new List<OtpRequest> { existingOtp });

        _userServiceMock.Setup(x => x.UpdatePasswordAsync(userDto.Id, It.IsAny<string>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0003, null!, true));

        // Act
        var result = await _sut.ResetPasswordAsync(email, otp, newPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
        _userServiceMock.Verify(x => x.UpdatePasswordAsync(userDto.Id, It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: ResetPasswordAsync when password update fails in DB
    /// Precondition: Valid inputs but database update returns failure
    /// Expected Result: Returns failure SYS_Fail0001
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_UpdateFails_ReturnsFailure()
    {
        // Arrange
        var email = "test@example.com";
        var otp = "123456";
        var newPassword = "NewPassword123!";
        
        var userDto = CreateValidUserDto();
        userDto.Email = email;

        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));

        var existingOtp = new OtpRequest
        {
            Id = Guid.NewGuid(),
            UserId = userDto.Id,
            Code = otp,
            Type = OtpType.ResetPassword,
            IsUsed = false,
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiredAt = DateTime.UtcNow.AddMinutes(2)
        };

        _otpRepoMock.Setup(x => x.GetAllAsync(It.IsAny<bool>()))
            .ReturnsAsync(new List<OtpRequest> { existingOtp });

        _userServiceMock.Setup(x => x.UpdatePasswordAsync(userDto.Id, It.IsAny<string>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Fail0001, null!, false));

        // Act
        var result = await _sut.ResetPasswordAsync(email, otp, newPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Fail0001);
    }

    #endregion

}
