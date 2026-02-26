using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Configurations;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Dtos.User;
using SFARS.Application.Services;
using SFARS.Application.Services.Auth;
using SFARS.Application.Utils;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Models;

namespace SFARS.Tests.Application.Services.Auth;

/// <summary>
/// Unit tests for the OTP system: SendOtpAsync, VerifyOtpAsync, and refactored ResetPasswordAsync
/// </summary>
public class OtpServiceTests
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
    private readonly AuthService _sut;

    // Shared test data
    private readonly Guid _testUserId = Guid.NewGuid();
    private const string TestEmail = "test@example.com";

    public OtpServiceTests()
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

        // Setup JwtUtils
        _jwtUtilsMock.Setup(x => x.GenerateJwtTokenAsync(It.IsAny<string>(), It.IsAny<AuthUserDto>()))
            .ReturnsAsync(("test-access-token", DateTime.UtcNow.AddHours(1)));
        _jwtUtilsMock.Setup(x => x.GenerateRefreshTokenAsync())
            .ReturnsAsync("test-refresh-token");

        // Setup UnitOfWork to return the OtpRequest repository mock
        _unitOfWorkMock.Setup(x => x.Repository<OtpRequest, Guid>())
            .Returns(_otpRepoMock.Object);

        // Default: SaveChangesAsync succeeds
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

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
            _tokenBlacklistServiceMock.Object
        );
    }

    #region SendOtpAsync Tests

    [Fact]
    public async Task SendOtpAsync_EmptyEmail_ReturnsValidationWarning()
    {
        // Arrange & Act
        var result = await _sut.SendOtpAsync("", "RESET_PASSWORD");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
    }

    [Fact]
    public async Task SendOtpAsync_InvalidPurpose_ReturnsValidationWarning()
    {
        // Arrange & Act
        var result = await _sut.SendOtpAsync(TestEmail, "INVALID_PURPOSE");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
    }

    [Fact]
    public async Task SendOtpAsync_UserNotFound_ReturnsWarning()
    {
        // Arrange
        _userServiceMock.Setup(x => x.GetByEmailAsync(TestEmail))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0002, "Not found", null!));

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, "RESET_PASSWORD");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
    }

    [Fact]
    public async Task SendOtpAsync_InactiveUser_ReturnsWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        userDto.Status = UserStatus.Inactive;
        SetupUserFound(userDto);

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, "RESET_PASSWORD");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0001);
    }

    [Fact]
    public async Task SendOtpAsync_LockedDueToMaxAttempts_ReturnsLockWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        // Simulate locked OTP (max attempts reached recently)
        var lockedOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.ResetPassword,
            attemptCount: OtpConstants.MaxAttempts,
            createdMinutesAgo: 5); // within lockout period

        SetupOtpRepository(new List<OtpRequest> { lockedOtp });

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, "RESET_PASSWORD");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0015);
    }

    [Fact]
    public async Task SendOtpAsync_CooldownNotExpired_ReturnsCooldownWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        // Simulate recent OTP (within cooldown period)
        var recentOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.ResetPassword,
            createdMinutesAgo: 0); // just created

        SetupOtpRepository(new List<OtpRequest> { recentOtp });

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, "RESET_PASSWORD");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0016);
    }

    [Fact]
    public async Task SendOtpAsync_ValidRequest_SendsEmailAndReturnsSuccess()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);
        SetupOtpRepository(new List<OtpRequest>()); // no existing OTPs

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<EmailMessageDto>(), true))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, "RESET_PASSWORD");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0005);
        _emailServiceMock.Verify(x => x.SendEmailAsync(
            It.Is<EmailMessageDto>(m => m.To == TestEmail), true), Times.Once);
        _otpRepoMock.Verify(x => x.AddAsync(It.IsAny<OtpRequest>()), Times.Once);
    }

    [Fact]
    public async Task SendOtpAsync_EmailFails_ReturnsFailure()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);
        SetupOtpRepository(new List<OtpRequest>());

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<EmailMessageDto>(), true))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, "RESET_PASSWORD");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Fail0002);
    }

    [Fact]
    public async Task SendOtpAsync_InvalidatesOldOtps_BeforeCreatingNew()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        // Old unused OTP that's past cooldown
        var oldOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.ResetPassword,
            createdMinutesAgo: 5,
            isUsed: false);

        SetupOtpRepository(new List<OtpRequest> { oldOtp });

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<EmailMessageDto>(), true))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, "RESET_PASSWORD");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0005);
        oldOtp.IsUsed.Should().BeTrue(); // old OTP should be invalidated
        _otpRepoMock.Verify(x => x.UpdateAsync(It.Is<OtpRequest>(o => o.Id == oldOtp.Id)), Times.Once);
    }

    [Fact]
    public async Task SendOtpAsync_PurposeIsolation_SignInDoesNotAffectResetPassword()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        // Existing OTP for SIGN_IN purpose (should NOT block RESET_PASSWORD)
        var signInOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.SignIn,
            createdMinutesAgo: 0); // within cooldown for SIGN_IN

        SetupOtpRepository(new List<OtpRequest> { signInOtp });

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<EmailMessageDto>(), true))
            .ReturnsAsync(true);

        // Act - Send RESET_PASSWORD OTP
        var result = await _sut.SendOtpAsync(TestEmail, "RESET_PASSWORD");

        // Assert - Should succeed because SIGN_IN cooldown doesn't affect RESET_PASSWORD
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0005);
    }

    #endregion

    #region VerifyOtpAsync Tests

    [Fact]
    public async Task VerifyOtpAsync_EmptyInputs_ReturnsValidationWarning()
    {
        // Arrange & Act
        var result = await _sut.VerifyOtpAsync("", "", "");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
    }

    [Fact]
    public async Task VerifyOtpAsync_UserNotFound_ReturnsWarning()
    {
        // Arrange
        _userServiceMock.Setup(x => x.GetByEmailAsync(TestEmail))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0002, "Not found", null!));

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", "RESET_PASSWORD");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
    }

    [Fact]
    public async Task VerifyOtpAsync_NoOtpFound_ReturnsNotFoundWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);
        SetupOtpRepository(new List<OtpRequest>()); // no OTPs

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", "RESET_PASSWORD");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0017);
    }

    [Fact]
    public async Task VerifyOtpAsync_ExpiredOtp_ReturnsExpiredWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var expiredOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.ResetPassword,
            code: "123456",
            createdMinutesAgo: 10,
            expiredMinutesFromNow: -5); // already expired

        SetupOtpRepository(new List<OtpRequest> { expiredOtp });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", "RESET_PASSWORD");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0014);
    }

    [Fact]
    public async Task VerifyOtpAsync_MaxAttemptsReached_ReturnsLockedWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var lockedOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.ResetPassword,
            code: "123456",
            attemptCount: OtpConstants.MaxAttempts);

        SetupOtpRepository(new List<OtpRequest> { lockedOtp });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", "RESET_PASSWORD");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0015);
    }

    [Fact]
    public async Task VerifyOtpAsync_WrongCode_IncrementsAttemptCount()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var otpRequest = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.ResetPassword,
            code: "123456",
            attemptCount: 0);

        SetupOtpRepository(new List<OtpRequest> { otpRequest });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "wrong-code", "RESET_PASSWORD");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0005);
        otpRequest.AttemptCount.Should().Be(1);
        _otpRepoMock.Verify(x => x.UpdateAsync(It.Is<OtpRequest>(o => o.AttemptCount == 1)), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task VerifyOtpAsync_CorrectCode_ReturnsSuccess()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var validOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.ResetPassword,
            code: "123456");

        SetupOtpRepository(new List<OtpRequest> { validOtp });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", "RESET_PASSWORD");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0010);
    }

    [Fact]
    public async Task VerifyOtpAsync_SignIn_MarksOtpAsUsed()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var signInOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.SignIn,
            code: "123456");

        SetupOtpRepository(new List<OtpRequest> { signInOtp });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", "SIGN_IN");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0010);
        signInOtp.IsUsed.Should().BeTrue(); // SIGN_IN marks used immediately
    }

    [Fact]
    public async Task VerifyOtpAsync_ResetPassword_DoesNotMarkOtpAsUsed()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var resetOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.ResetPassword,
            code: "123456");

        SetupOtpRepository(new List<OtpRequest> { resetOtp });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", "RESET_PASSWORD");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0010);
        resetOtp.IsUsed.Should().BeFalse(); // RESET_PASSWORD keeps it for ResetPasswordAsync
    }

    [Fact]
    public async Task VerifyOtpAsync_PurposeIsolation_SignInOtpCannotVerifyResetPassword()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        // Only a SIGN_IN OTP exists
        var signInOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.SignIn,
            code: "123456");

        SetupOtpRepository(new List<OtpRequest> { signInOtp });

        // Act - Try to verify with RESET_PASSWORD purpose
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", "RESET_PASSWORD");

        // Assert - Should fail because no RESET_PASSWORD OTP exists
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0017);
    }

    [Fact]
    public async Task VerifyOtpAsync_UsedOtpIsSkipped_ReturnsNotFound()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        // OTP that was already used
        var usedOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.ResetPassword,
            code: "123456",
            isUsed: true);

        SetupOtpRepository(new List<OtpRequest> { usedOtp });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", "RESET_PASSWORD");

        // Assert - used OTPs should be filtered out
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0017);
    }

    #endregion

    #region ResetPasswordAsync (with OtpRequest) Tests

    [Fact]
    public async Task ResetPasswordAsync_ValidOtp_UpdatesPasswordAndMarksOtpUsed()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var validOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.ResetPassword,
            code: "123456");

        SetupOtpRepository(new List<OtpRequest> { validOtp });

        _userServiceMock.Setup(x => x.UpdatePasswordAsync(userDto.Id, It.IsAny<string>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0003, null!, true));

        // Act
        var result = await _sut.ResetPasswordAsync(TestEmail, "123456", "NewPassword123!");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
        validOtp.IsUsed.Should().BeTrue();
        _userServiceMock.Verify(x => x.UpdatePasswordAsync(userDto.Id, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_NoOtpRequest_ReturnsNotFoundWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);
        SetupOtpRepository(new List<OtpRequest>()); // no OTPs

        // Act
        var result = await _sut.ResetPasswordAsync(TestEmail, "123456", "NewPassword123!");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0017);
    }

    [Fact]
    public async Task ResetPasswordAsync_ExpiredOtp_ReturnsExpiredWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var expiredOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.ResetPassword,
            code: "123456",
            expiredMinutesFromNow: -5);

        SetupOtpRepository(new List<OtpRequest> { expiredOtp });

        // Act
        var result = await _sut.ResetPasswordAsync(TestEmail, "123456", "NewPassword123!");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0014);
    }

    [Fact]
    public async Task ResetPasswordAsync_WrongOtp_IncrementsAttemptAndReturnsWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var otpRequest = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.ResetPassword,
            code: "123456",
            attemptCount: 0);

        SetupOtpRepository(new List<OtpRequest> { otpRequest });

        // Act
        var result = await _sut.ResetPasswordAsync(TestEmail, "wrong-otp", "NewPassword123!");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0005);
        otpRequest.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task ResetPasswordAsync_SameAsOldPassword_ReturnsWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        var password = "ExistingPassword123!";
        userDto.PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(password);
        SetupUserFound(userDto);

        var validOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.ResetPassword,
            code: "123456");

        SetupOtpRepository(new List<OtpRequest> { validOtp });

        // Act - try to "reset" with the same password
        var result = await _sut.ResetPasswordAsync(TestEmail, "123456", password);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0011);
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidatesRemainingOtps_AfterSuccessfulReset()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var validOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.ResetPassword,
            code: "123456");

        var otherOtp = CreateOtpRequest(
            userId: userDto.Id,
            purpose: OtpPurpose.ResetPassword,
            code: "654321",
            createdMinutesAgo: 5);

        SetupOtpRepository(new List<OtpRequest> { validOtp, otherOtp });

        _userServiceMock.Setup(x => x.UpdatePasswordAsync(userDto.Id, It.IsAny<string>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0003, null!, true));

        // Act
        var result = await _sut.ResetPasswordAsync(TestEmail, "123456", "NewPassword123!");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
        validOtp.IsUsed.Should().BeTrue();
        otherOtp.IsUsed.Should().BeTrue(); // remaining OTPs also invalidated
    }

    #endregion

    #region ForgotPasswordAsync (delegates to SendOtpAsync) Tests

    [Fact]
    public async Task ForgotPasswordAsync_ValidEmail_DelegatesToSendOtpWithResetPasswordPurpose()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);
        SetupOtpRepository(new List<OtpRequest>());

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<EmailMessageDto>(), true))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ForgotPasswordAsync(TestEmail);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0005);
        _emailServiceMock.Verify(x => x.SendEmailAsync(
            It.Is<EmailMessageDto>(m => m.To == TestEmail), true), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_UserNotFound_ReturnsWarning()
    {
        // Arrange
        _userServiceMock.Setup(x => x.GetByEmailAsync(TestEmail))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0002, "Not found", null!));

        // Act
        var result = await _sut.ForgotPasswordAsync(TestEmail);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
    }

    #endregion

    #region Helper Methods

    private UserDto CreateValidUserDto()
    {
        return new UserDto
        {
            Id = _testUserId,
            Email = TestEmail,
            FirstName = "Test",
            LastName = "User",
            Status = UserStatus.Active,
            TwoFactorEnabled = false,
            Role = "User"
        };
    }

    private void SetupUserFound(UserDto userDto)
    {
        _userServiceMock.Setup(x => x.GetByEmailAsync(userDto.Email))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, null!, userDto));
    }

    private void SetupOtpRepository(List<OtpRequest> existingOtps)
    {
        _otpRepoMock.Setup(x => x.GetAllAsync(It.IsAny<bool>()))
            .ReturnsAsync(existingOtps.AsEnumerable());
    }

    private OtpRequest CreateOtpRequest(
        Guid userId,
        OtpPurpose purpose,
        string code = "123456",
        int attemptCount = 0,
        bool isUsed = false,
        int createdMinutesAgo = 1,
        int? expiredMinutesFromNow = null)
    {
        return new OtpRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = code,
            Purpose = purpose,
            AttemptCount = attemptCount,
            IsUsed = isUsed,
            CreatedAt = DateTime.UtcNow.AddMinutes(-createdMinutesAgo),
            ExpiredAt = expiredMinutesFromNow.HasValue
                ? DateTime.UtcNow.AddMinutes(expiredMinutesFromNow.Value)
                : DateTime.UtcNow.AddMinutes(OtpConstants.OtpExpirationMinutes)
        };
    }

    #endregion
}
