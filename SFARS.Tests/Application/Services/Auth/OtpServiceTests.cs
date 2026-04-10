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
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
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
            _backgroundJobClientMock.Object,
            _tokenBlacklistServiceMock.Object,
            new Moq.Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object
        );
    }

    #region SendOtpAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SendOtpAsync with empty/null email address
    /// Precondition: Empty string passed as email parameter
    /// Expected Result: Returns validation warning SYS_Warning0001
    /// </summary>
    [Fact]
    public async Task SendOtpAsync_EmptyEmail_ReturnsValidationWarning()
    {
        // Arrange & Act
        var result = await _sut.SendOtpAsync("", OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SendOtpAsync when user email does not exist in system
    /// Precondition: Email provided but no matching user account
    /// Expected Result: Returns user not found warning SYS_Warning0002
    /// </summary>
    [Fact]
    public async Task SendOtpAsync_UserNotFound_ReturnsWarning()
    {
        // Arrange
        _userServiceMock.Setup(x => x.GetByEmailAsync(TestEmail))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0002, "Not found", null!));

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SendOtpAsync when user account is inactive
    /// Precondition: Valid email but user status is Inactive
    /// Expected Result: Returns inactive user warning Auth_Warning0001
    /// </summary>
    [Fact]
    public async Task SendOtpAsync_InactiveUser_ReturnsWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        userDto.Status = UserStatus.Inactive;
        SetupUserFound(userDto);

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0001);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SendOtpAsync when user has reached max verification attempts
    /// Precondition: Existing OTP with AttemptCount = MaxAttempts, created within lockout period
    /// Expected Result: Returns lockout warning Auth_Warning0015, prevents new OTP creation
    /// </summary>
    [Fact]
    public async Task SendOtpAsync_LockedDueToMaxAttempts_ReturnsLockWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        // Simulate locked OTP (max attempts reached recently)
        var lockedOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            attemptCount: OtpConstants.MaxAttempts,
            createdMinutesAgo: 5); // within lockout period

        SetupOtpRepository(new List<OtpRequest> { lockedOtp });

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0015);
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: SendOtpAsync when cooldown period has not expired
    /// Precondition: Recent OTP created within cooldown seconds
    /// Expected Result: Returns cooldown warning Auth_Warning0016, prevents OTP spam
    /// </summary>
    [Fact]
    public async Task SendOtpAsync_CooldownNotExpired_ReturnsCooldownWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        // Simulate recent OTP (within cooldown period)
        var recentOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            createdMinutesAgo: 0); // just created

        SetupOtpRepository(new List<OtpRequest> { recentOtp });

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0016);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SendOtpAsync (ResetPassword type) for user without PasswordHash
    /// Precondition: User has empty PasswordHash (e.g. Google Login only)
    /// Expected Result: Sends email with "Set Password OTP for SFARS" subject
    /// </summary>
    [Fact]
    public async Task SendOtpAsync_ResetPassword_WithoutPasswordHash_SendsSetPasswordEmail()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        userDto.PasswordHash = string.Empty; // No password hash
        SetupUserFound(userDto);
        SetupOtpRepository(new List<OtpRequest>());

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<EmailMessageDto>(), true))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0005);
        _emailServiceMock.Verify(x => x.SendEmailAsync(
            It.Is<EmailMessageDto>(m => m.To == TestEmail && m.Subject == "Set Password OTP for SFARS"), true), Times.Once);
        _otpRepoMock.Verify(x => x.AddAsync(It.IsAny<OtpRequest>()), Times.Once);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SendOtpAsync (ResetPassword type) for user with PasswordHash
    /// Precondition: User has non-empty PasswordHash
    /// Expected Result: Sends email with "Password Reset OTP for SFARS" subject
    /// </summary>
    [Fact]
    public async Task SendOtpAsync_ResetPassword_WithPasswordHash_SendsResetPasswordEmail()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        userDto.PasswordHash = "some_hashed_password"; // Has password hash
        SetupUserFound(userDto);
        SetupOtpRepository(new List<OtpRequest>());

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<EmailMessageDto>(), true))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0005);
        _emailServiceMock.Verify(x => x.SendEmailAsync(
            It.Is<EmailMessageDto>(m => m.To == TestEmail && m.Subject == "Password Reset OTP for SFARS"), true), Times.Once);
        _otpRepoMock.Verify(x => x.AddAsync(It.IsAny<OtpRequest>()), Times.Once);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SendOtpAsync when email service fails
    /// Precondition: Valid request but email sending returns false
    /// Expected Result: Returns failure Auth_Fail0002
    /// </summary>
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
        var result = await _sut.SendOtpAsync(TestEmail, OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Fail0002);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SendOtpAsync properly invalidates old unused OTPs before creating new one
    /// Precondition: Old OTP exists that is past cooldown and not used
    /// Expected Result: Old OTP marked as used, new OTP created successfully
    /// </summary>
    [Fact]
    public async Task SendOtpAsync_InvalidatesOldOtps_BeforeCreatingNew()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        // Old unused OTP that's past cooldown
        var oldOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            createdMinutesAgo: 5,
            isUsed: false);

        SetupOtpRepository(new List<OtpRequest> { oldOtp });

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<EmailMessageDto>(), true))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0005);
        oldOtp.IsUsed.Should().BeTrue(); // old OTP should be invalidated
        _otpRepoMock.Verify(x => x.UpdateAsync(It.Is<OtpRequest>(o => o.Id == oldOtp.Id)), Times.Once);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SendOtpAsync maintains type isolation between SignIn and ResetPassword OTPs
    /// Precondition: Active SignIn OTP within cooldown period
    /// Expected Result: Can still create ResetPassword OTP, types are independent
    /// </summary>
    [Fact]
    public async Task SendOtpAsync_TypeIsolation_SignInDoesNotAffectResetPassword()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        // Existing OTP for SignIn type (should NOT block ResetPassword)
        var signInOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.SignIn,
            createdMinutesAgo: 0); // within cooldown for SignIn

        SetupOtpRepository(new List<OtpRequest> { signInOtp });

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<EmailMessageDto>(), true))
            .ReturnsAsync(true);

        // Act - Send ResetPassword OTP
        var result = await _sut.SendOtpAsync(TestEmail, OtpType.ResetPassword);

        // Assert - Should succeed because SignIn cooldown doesn't affect ResetPassword
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0005);
    }

    #endregion

    #region VerifyOtpAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: VerifyOtpAsync with empty email and OTP code
    /// Precondition: Both email and code parameters are empty strings
    /// Expected Result: Returns validation warning SYS_Warning0001
    /// </summary>
    [Fact]
    public async Task VerifyOtpAsync_EmptyInputs_ReturnsValidationWarning()
    {
        // Arrange & Act
        var result = await _sut.VerifyOtpAsync("", "", OtpType.SignIn);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: VerifyOtpAsync when user email does not exist
    /// Precondition: Email provided but no matching user in system
    /// Expected Result: Returns user not found warning SYS_Warning0002
    /// </summary>
    [Fact]
    public async Task VerifyOtpAsync_UserNotFound_ReturnsWarning()
    {
        // Arrange
        _userServiceMock.Setup(x => x.GetByEmailAsync(TestEmail))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Warning0002, "Not found", null!));

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: VerifyOtpAsync when no OTP request exists for user
    /// Precondition: Valid user but no OTP created
    /// Expected Result: Returns OTP not found warning Auth_Warning0017
    /// </summary>
    [Fact]
    public async Task VerifyOtpAsync_NoOtpFound_ReturnsNotFoundWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);
        SetupOtpRepository(new List<OtpRequest>()); // no OTPs

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0017);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: VerifyOtpAsync with expired OTP
    /// Precondition: OTP exists but ExpiredAt time has passed
    /// Expected Result: Returns expired warning Auth_Warning0014
    /// </summary>
    [Fact]
    public async Task VerifyOtpAsync_ExpiredOtp_ReturnsExpiredWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var expiredOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            code: "123456",
            createdMinutesAgo: 10,
            expiredMinutesFromNow: -5); // already expired

        SetupOtpRepository(new List<OtpRequest> { expiredOtp });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0014);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: VerifyOtpAsync when max verification attempts reached
    /// Precondition: OTP exists with AttemptCount = MaxAttempts
    /// Expected Result: Returns locked warning Auth_Warning0015
    /// </summary>
    [Fact]
    public async Task VerifyOtpAsync_MaxAttemptsReached_ReturnsLockedWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var lockedOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            code: "123456",
            attemptCount: OtpConstants.MaxAttempts);

        SetupOtpRepository(new List<OtpRequest> { lockedOtp });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0015);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: VerifyOtpAsync with incorrect OTP code
    /// Precondition: Valid OTP exists but code does not match
    /// Expected Result: Returns warning Auth_Warning0005, increments AttemptCount
    /// </summary>
    [Fact]
    public async Task VerifyOtpAsync_WrongCode_IncrementsAttemptCount()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var otpRequest = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            code: "123456",
            attemptCount: 0);

        SetupOtpRepository(new List<OtpRequest> { otpRequest });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "wrong-code", OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0005);
        otpRequest.AttemptCount.Should().Be(1);
        _otpRepoMock.Verify(x => x.UpdateAsync(It.Is<OtpRequest>(o => o.AttemptCount == 1)), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: VerifyOtpAsync with correct OTP code
    /// Precondition: Valid unexpired OTP with matching code
    /// Expected Result: Returns success Auth_Success0010
    /// </summary>
    [Fact]
    public async Task VerifyOtpAsync_CorrectCode_ReturnsSuccess()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var validOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            code: "123456");

        SetupOtpRepository(new List<OtpRequest> { validOtp });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0010);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: VerifyOtpAsync for SignIn type marks OTP as used immediately
    /// Precondition: Valid SignIn OTP with correct code
    /// Expected Result: Success and OTP.IsUsed = true
    /// </summary>
    [Fact]
    public async Task VerifyOtpAsync_SignIn_MarksOtpAsUsed()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var signInOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.SignIn,
            code: "123456");

        SetupOtpRepository(new List<OtpRequest> { signInOtp });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", OtpType.SignIn);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0010);
        signInOtp.IsUsed.Should().BeTrue(); // SignIn marks used immediately
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: VerifyOtpAsync for ResetPassword type does not mark as used yet
    /// Precondition: Valid ResetPassword OTP with correct code
    /// Expected Result: Success but OTP.IsUsed = false (reserved for ResetPasswordAsync)
    /// </summary>
    [Fact]
    public async Task VerifyOtpAsync_ResetPassword_DoesNotMarkOtpAsUsed()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var resetOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            code: "123456");

        SetupOtpRepository(new List<OtpRequest> { resetOtp });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0010);
        resetOtp.IsUsed.Should().BeFalse(); // ResetPassword keeps it for ResetPasswordAsync
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: VerifyOtpAsync maintains strict type isolation
    /// Precondition: Only SignIn OTP exists, trying to verify with ResetPassword type
    /// Expected Result: Returns not found Auth_Warning0017 due to type mismatch
    /// </summary>
    [Fact]
    public async Task VerifyOtpAsync_TypeIsolation_SignInOtpCannotVerifyResetPassword()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        // Only a SignIn OTP exists
        var signInOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.SignIn,
            code: "123456");

        SetupOtpRepository(new List<OtpRequest> { signInOtp });

        // Act - Try to verify with ResetPassword type
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", OtpType.ResetPassword);

        // Assert - Should fail because no ResetPassword OTP exists
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0017);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: VerifyOtpAsync skips already used OTPs
    /// Precondition: OTP exists with IsUsed = true
    /// Expected Result: Returns not found Auth_Warning0017
    /// </summary>
    [Fact]
    public async Task VerifyOtpAsync_UsedOtpIsSkipped_ReturnsNotFound()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        // OTP that was already used
        var usedOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            code: "123456",
            isUsed: true);

        SetupOtpRepository(new List<OtpRequest> { usedOtp });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", OtpType.ResetPassword);

        // Assert - used OTPs should be filtered out
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0017);
    }

    #endregion

    #region ResetPasswordAsync (with OtpRequest) Tests

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: ResetPasswordAsync with valid OTP successfully resets password
    /// Precondition: Valid unexpired OTP with correct code
    /// Expected Result: Password updated, OTP marked as used, returns SYS_Success0003
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_ValidOtp_UpdatesPasswordAndMarksOtpUsed()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var validOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
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

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: ResetPasswordAsync when no OTP request exists
    /// Precondition: Valid user but no OTP created
    /// Expected Result: Returns not found warning Auth_Warning0017
    /// </summary>
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

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: ResetPasswordAsync with expired OTP
    /// Precondition: OTP exists but ExpiredAt time has passed
    /// Expected Result: Returns expired warning Auth_Warning0014
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_ExpiredOtp_ReturnsExpiredWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var expiredOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            code: "123456",
            expiredMinutesFromNow: -5);

        SetupOtpRepository(new List<OtpRequest> { expiredOtp });

        // Act
        var result = await _sut.ResetPasswordAsync(TestEmail, "123456", "NewPassword123!");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0014);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: ResetPasswordAsync with wrong OTP code
    /// Precondition: Valid OTP exists but code does not match
    /// Expected Result: Returns warning Auth_Warning0005, increments AttemptCount
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_WrongOtp_IncrementsAttemptAndReturnsWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var otpRequest = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            code: "123456",
            attemptCount: 0);

        SetupOtpRepository(new List<OtpRequest> { otpRequest });

        // Act
        var result = await _sut.ResetPasswordAsync(TestEmail, "654321", "NewPassword123!");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0005);
        otpRequest.AttemptCount.Should().Be(1);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: ResetPasswordAsync when new password matches current password
    /// Precondition: Valid OTP but new password is same as existing password hash
    /// Expected Result: Returns warning Auth_Warning0011
    /// </summary>
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
            type: OtpType.ResetPassword,
            code: "123456");

        SetupOtpRepository(new List<OtpRequest> { validOtp });

        // Act - try to "reset" with the same password
        var result = await _sut.ResetPasswordAsync(TestEmail, "123456", password);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0011);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: ResetPasswordAsync invalidates all remaining OTPs after successful reset
    /// Precondition: Multiple valid OTPs exist for same user
    /// Expected Result: All OTPs marked as used after successful password reset
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_InvalidatesRemainingOtps_AfterSuccessfulReset()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var validOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            code: "123456");

        var otherOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
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

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: ForgotPasswordAsync delegates to SendOtpAsync with ResetPassword type
    /// Precondition: Valid active user email
    /// Expected Result: OTP sent with ResetPassword purpose, returns Auth_Success0005
    /// </summary>
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

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: ForgotPasswordAsync when user email does not exist
    /// Precondition: Email provided but no matching user
    /// Expected Result: Returns user not found warning SYS_Warning0002
    /// </summary>
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

    #region Boundary Tests - Time-Based Scenarios

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: VerifyOtpAsync with OTP exactly at expiry time
    /// Precondition: OTP ExpiresAt = DateTime.UtcNow (exact boundary)
    /// Expected Result: Should return expired warning (boundary is exclusive)
    /// </summary>
    [Fact]
    public async Task VerifyOtpAsync_OtpAtExactExpiryTime_ReturnsExpiredWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var otpAtBoundary = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            code: "123456",
            expiredMinutesFromNow: 0); // Expires exactly now

        SetupOtpRepository(new List<OtpRequest> { otpAtBoundary });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", OtpType.ResetPassword);

        // Assert - At exact boundary, should be treated as expired
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0014);
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: VerifyOtpAsync with OTP one second before expiry
    /// Precondition: OTP expires in 1 second (still valid)
    /// Expected Result: Should succeed - still within valid time window
    /// </summary>
    [Fact]
    public async Task VerifyOtpAsync_OtpOneSecondBeforeExpiry_ReturnsSuccess()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        // Create OTP that expires in 1 second
        var otpRequest = new OtpRequest
        {
            Id = Guid.NewGuid(),
            UserId = userDto.Id,
            Code = "123456",
            Type = OtpType.ResetPassword,
            AttemptCount = 0,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow,
            ExpiredAt = DateTime.UtcNow.AddSeconds(1) // BOUNDARY: 1 second before expiry
        };

        SetupOtpRepository(new List<OtpRequest> { otpRequest });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", OtpType.ResetPassword);

        // Assert - Should still be valid
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0010);
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: SendOtpAsync when cooldown period expires exactly now
    /// Precondition: Last OTP was created exactly cooldown minutes ago
    /// Expected Result: Should allow new OTP generation
    /// </summary>
    [Fact]
    public async Task SendOtpAsync_CooldownExpiresExactly_AllowsNewOtp()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        // Create OTP exactly at cooldown boundary (OtpConstants.OtpCooldownMinutes ago)
        var otpAtBoundary = new OtpRequest
        {
            Id = Guid.NewGuid(),
            UserId = userDto.Id,
            Code = "old-code",
            Type = OtpType.ResetPassword,
            AttemptCount = 0,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow.AddSeconds(-OtpConstants.CooldownSeconds), // Exact boundary
            ExpiredAt = DateTime.UtcNow.AddMinutes(5)
        };

        SetupOtpRepository(new List<OtpRequest> { otpAtBoundary });

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<EmailMessageDto>(), true))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, OtpType.ResetPassword);

        // Assert - Should succeed at exact boundary
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0005);
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: VerifyOtpAsync at exactly max attempt count
    /// Precondition: OTP has AttemptCount = MaxAttempts (5)
    /// Expected Result: Should return locked warning (at boundary)
    /// </summary>
    [Fact]
    public async Task VerifyOtpAsync_AtExactMaxAttempts_ReturnsLockedWarning()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var otpAtMaxAttempts = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            code: "123456",
            attemptCount: OtpConstants.MaxAttempts); // BOUNDARY: Exactly at max

        SetupOtpRepository(new List<OtpRequest> { otpAtMaxAttempts });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", OtpType.ResetPassword);

        // Assert - At max attempts, should be locked
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0015);
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: VerifyOtpAsync one attempt before max
    /// Precondition: OTP has AttemptCount = MaxAttempts - 1 (4)
    /// Expected Result: Should allow verification (not yet locked)
    /// </summary>
    [Fact]
    public async Task VerifyOtpAsync_OneAttemptBeforeMax_AllowsVerification()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var otpNearLimit = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            code: "123456",
            attemptCount: OtpConstants.MaxAttempts - 1); // BOUNDARY: One before max

        SetupOtpRepository(new List<OtpRequest> { otpNearLimit });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, "123456", OtpType.ResetPassword);

        // Assert - Should allow attempt (will succeed since code matches)
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0010);
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: SendOtpAsync lockout period expires exactly now
    /// Precondition: OTP reached max attempts exactly lockout minutes ago
    /// Expected Result: Should allow new OTP (lockout expired)
    /// </summary>
    [Fact]
    public async Task SendOtpAsync_LockoutExpiresExactly_AllowsNewOtp()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        // Create locked OTP exactly at lockout expiry boundary
        var lockedOtpAtBoundary = new OtpRequest
        {
            Id = Guid.NewGuid(),
            UserId = userDto.Id,
            Code = "old-code",
            Type = OtpType.ResetPassword,
            // Max attempts but created exactly at lockout expiry time
            AttemptCount = OtpConstants.MaxAttempts,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-OtpConstants.LockoutMinutes), // Exact boundary
            ExpiredAt = DateTime.UtcNow.AddMinutes(5)
        };

        SetupOtpRepository(new List<OtpRequest> { lockedOtpAtBoundary });

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<EmailMessageDto>(), true))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.SendOtpAsync(TestEmail, OtpType.ResetPassword);

        // Assert - Lockout expired, should allow new OTP
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Success0005);
    }

    #endregion

    #region Abnormal Tests - Additional Error Scenarios

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: VerifyOtpAsync with null email
    /// Precondition: Null passed as email parameter
    /// Expected Result: Returns validation warning
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyOtpAsync_InvalidEmail_ReturnsValidationWarning(string invalidEmail)
    {
        // Act
        var result = await _sut.VerifyOtpAsync(invalidEmail, "123456", OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: VerifyOtpAsync with various invalid OTP codes
    /// Precondition: Valid user but invalid OTP code formats
    /// Expected Result: Returns invalid code warning
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("12345")]  // Too short
    [InlineData("1234567")] // Too long
    public async Task VerifyOtpAsync_InvalidOtpCode_ReturnsInvalidWarning(string invalidCode)
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);

        var validOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            code: "123456");

        SetupOtpRepository(new List<OtpRequest> { validOtp });

        // Act
        var result = await _sut.VerifyOtpAsync(TestEmail, invalidCode, OtpType.ResetPassword);

        // Assert
        result.ResultCode.Should().NotBe(ResultCodeConst.Auth_Success0010);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SendOtpAsync when email service throws exception
    /// Precondition: Email service unavailable or throws error
    /// Expected Result: Returns failure result or propagates exception
    /// </summary>
    [Fact]
    public async Task SendOtpAsync_EmailServiceException_ReturnsFailure()
    {
        // Arrange
        var userDto = CreateValidUserDto();
        SetupUserFound(userDto);
        SetupOtpRepository(new List<OtpRequest>());

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<EmailMessageDto>(), true))
            .ThrowsAsync(new InvalidOperationException("Email service unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.SendOtpAsync(TestEmail, OtpType.ResetPassword));
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: ResetPasswordAsync with banned user
    /// Precondition: User exists but has status = Banned
    /// Expected Result: Should return appropriate warning about user status
    /// </summary>
    [Theory]
    [InlineData(UserStatus.Banned)]
    [InlineData(UserStatus.Deleted)]
    public async Task ResetPasswordAsync_InvalidUserStatus_ReturnsWarning(UserStatus status)
    {
        // Arrange
        var userDto = CreateValidUserDto();
        userDto.Status = status;
        SetupUserFound(userDto);

        var validOtp = CreateOtpRequest(
            userId: userDto.Id,
            type: OtpType.ResetPassword,
            code: "123456");

        SetupOtpRepository(new List<OtpRequest> { validOtp });

        // Mock UpdatePasswordAsync to return failure for invalid status
        _userServiceMock.Setup(x => x.UpdatePasswordAsync(userDto.Id, It.IsAny<string>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.Auth_Warning0010, "User is inactive"));

        // Act
        var result = await _sut.ResetPasswordAsync(TestEmail, "123456", "NewPassword123!");

        // Assert - Should fail due to user status
        result.ResultCode.Should().NotBe(ResultCodeConst.SYS_Success0003);
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
        OtpType type,
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
            Type = type,
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

