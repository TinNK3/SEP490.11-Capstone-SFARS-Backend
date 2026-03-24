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
            _tokenBlacklistServiceMock.Object,
            new Moq.Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object
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
        // Arrange (token �? d�i �? qua validator)
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
        // Arrange
        var email = "notfound@test.com";
        
        _userServiceMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0002, "Not found", null!));

        // Act
        var result = await _sut.ForgotPasswordAsync(email);

        // Assert
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

    // ??????????????????????????????????????????????????????????????????????????????
    // Helper: build a real signed JWT so CanReadToken() + ReadJwtToken() work.
    // SignOutAsync parses the token inline (not via IJwtUtils) � tests must supply
    // an actual JWT string to exercise the blacklisting branch.
    // ??????????????????????????????????????????????????????????????????????????????
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

    // ?? STEP 1: Blacklisting ??????????????????????????????????????????????????????

    [Fact]
    public async Task SignOutAsync_WithValidJwt_RevokesJtiOnBlacklist()
    {
        // Arrange � real JWT so CanReadToken() returns true and JTI is extracted
        var userId = Guid.NewGuid();
        var expectedJti = Guid.NewGuid().ToString();
        var accessToken = BuildRealJwt(jti: expectedJti);

        _refreshTokenServiceMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0004, "Not found", null!));

        // Act
        await _sut.SignOutAsync(userId, accessToken);

        // Assert � Revoke must be called with the exact JTI extracted from the token
        _tokenBlacklistServiceMock.Verify(
            x => x.Revoke(expectedJti, It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task SignOutAsync_WithValidJwt_RevokesWithCorrectExpiry()
    {
        // Arrange � verify the expiry passed to Revoke matches the token's ValidTo
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

        // Assert � expiry forwarded to blacklist must match the token's actual ValidTo
        _tokenBlacklistServiceMock.Verify(
            x => x.Revoke(expectedJti, It.Is<DateTime>(d =>
                Math.Abs((d - parsedValidTo).TotalSeconds) < 2)),
            Times.Once);
    }

    [Fact]
    public async Task SignOutAsync_WithUnparsableToken_SkipsBlacklisting()
    {
        // Arrange � garbage string, CanReadToken() returns false; blacklist must be skipped
        var userId = Guid.NewGuid();
        const string garbageToken = "not.a.valid.jwt.at.all";

        _refreshTokenServiceMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0004, "Not found", null!));

        // Act
        await _sut.SignOutAsync(userId, garbageToken);

        // Assert � Revoke must never be called
        _tokenBlacklistServiceMock.Verify(
            x => x.Revoke(It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task SignOutAsync_WithJwtMissingJtiClaim_SkipsBlacklisting()
    {
        // Arrange � real JWT but no JTI claim; service should log warning and skip Revoke
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

    // ?? STEP 2: Refresh token deletion ???????????????????????????????????????????

    [Fact]
    public async Task SignOutAsync_WithValidJwt_ActiveSession_BlacklistsAndDeletesRefreshToken()
    {
        // Arrange � happy path: valid JWT + active refresh token
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

    [Fact]
    public async Task SignOutAsync_WithValidJwt_NoActiveSession_ReturnsSuccessWithoutDeletion()
    {
        // Arrange � no refresh token on record (already signed out / session expired)
        var userId = Guid.NewGuid();
        var accessToken = BuildRealJwt(jti: Guid.NewGuid().ToString());

        _refreshTokenServiceMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0004, "Not found", null!));

        // Act
        var result = await _sut.SignOutAsync(userId, accessToken);

        // Assert � still succeeds; token is blacklisted to prevent reuse
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0009);
        _refreshTokenServiceMock.Verify(x => x.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SignOutAsync_WithValidJwt_DeleteFails_ReturnsFailure()
    {
        // Arrange � blacklist succeeds but DB delete fails
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

        // Assert � JTI is still blacklisted even though delete failed
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Fail0001);
        result.Data.Should().BeNull();
        _tokenBlacklistServiceMock.Verify(x => x.Revoke(jti, It.IsAny<DateTime>()), Times.Once);
        _refreshTokenServiceMock.Verify(x => x.DeleteAsync(refreshTokenDto.Id), Times.Once);
    }

    [Fact]
    public async Task SignOutAsync_DeletesCorrectRefreshTokenId()
    {
        // Arrange � verifies DeleteAsync is called with the exact token ID from the lookup
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

    // ?? Exception paths ???????????????????????????????????????????????????????????

    [Fact]
    public async Task SignOutAsync_GetByUserIdThrowsException_PropagatesException()
    {
        // Arrange � DB failure during refresh token lookup
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

    [Fact]
    public async Task SignOutAsync_DeleteThrowsException_PropagatesException()
    {
        // Arrange � delete blows up (e.g. concurrency conflict)
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

    // ?? UserId routing ????????????????????????????????????????????????????????????

    [Fact]
    public async Task SignOutAsync_LooksUpCorrectUserId()
    {
        // Arrange � verify GetByUserIdAsync receives the exact userId passed in
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

    [Fact]
    public async Task ResetPasswordAsync_GoogleUser_CanSetPassword()
    {
        // Arrange - User ��ng k? qua Google (kh�ng c� password), mu?n �?t password m?i
        var email = "googleuser@test.com";
        var otp = "123456";
        var newPassword = "MyFirstPassword123!";
        
        var userDto = CreateValidUserDto();
        userDto.Email = email;
        userDto.PasswordHash = null; // Google user kh�ng c� password

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


