using FluentAssertions;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Rescuer;
using SFARS.Application.Services;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.Tests.Application.Services.Rescuer;

/// <summary>
/// Unit tests for RescuerService:
/// - GetRescuerProfileAsync    (GET  /api/rescuer/profile)
/// - UpdateRescuerProfileAsync (PUT  /api/rescuer/profile)
///
/// Scope: Application service layer - combined User + RescuerProfile management.
/// </summary>
public class RescuerServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<ILogger<RescuerService>> _loggerMock;
    private readonly Mock<ISystemMessageService> _msgServiceMock;
    private readonly Mock<IGenericRepository<User, Guid>> _userRepoMock;
    private readonly Mock<IGenericRepository<RescuerProfile, Guid>> _rescuerProfileRepoMock;

    private readonly RescuerService _sut;

    public RescuerServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mapperMock = new Mock<IMapper>();
        _loggerMock = new Mock<ILogger<RescuerService>>();
        _msgServiceMock = new Mock<ISystemMessageService>();
        _userRepoMock = new Mock<IGenericRepository<User, Guid>>();
        _rescuerProfileRepoMock = new Mock<IGenericRepository<RescuerProfile, Guid>>();

        _unitOfWorkMock.Setup(x => x.Repository<User, Guid>()).Returns(_userRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<RescuerProfile, Guid>()).Returns(_rescuerProfileRepoMock.Object);

        _msgServiceMock
            .Setup(x => x.GetMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((string code) => $"Message for {code}");

        _sut = new RescuerService(
            _unitOfWorkMock.Object,
            _mapperMock.Object,
            _loggerMock.Object,
            _msgServiceMock.Object
        );
    }

    #region GetRescuerProfileAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: GetRescuerProfileAsync with empty user ID
    /// Precondition: userId is Guid.Empty
    /// Expected Result: Returns Auth_Warning0007 (invalid user)
    /// </summary>
    [Fact]
    public async Task GetRescuerProfileAsync_EmptyUserId_ReturnsAuthWarning()
    {
        // Arrange
        var userId = Guid.Empty;

        // Act
        var result = await _sut.GetRescuerProfileAsync(userId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0007);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: GetRescuerProfileAsync when user does not exist
    /// Precondition: Valid GUID but no matching User entity
    /// Expected Result: Returns SYS_Warning0004 (not found)
    /// </summary>
    [Fact]
    public async Task GetRescuerProfileAsync_UserNotFound_ReturnsNotFoundWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync((User?)null);

        // Act
        var result = await _sut.GetRescuerProfileAsync(userId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: GetRescuerProfileAsync when RescuerProfile does not exist
    /// Precondition: User exists but RescuerProfile missing
    /// Expected Result: Returns SYS_Warning0004 (profile not found)
    /// </summary>
    [Fact]
    public async Task GetRescuerProfileAsync_ProfileNotFound_ReturnsNotFoundWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com"
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _rescuerProfileRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync((RescuerProfile?)null);

        // Act
        var result = await _sut.GetRescuerProfileAsync(userId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: GetRescuerProfileAsync returns combined User + RescuerProfile data
    /// Precondition: Valid user with complete rescuer profile
    /// Expected Result: Returns SYS_Success0002 with combined DTO
    /// </summary>
    [Fact]
    public async Task GetRescuerProfileAsync_ValidUser_ReturnsCombinedProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "+1234567890",
            Avatar = "https://example.com/avatar.jpg",
            Address = "123 Main St",
            Gender = Gender.Male,
            Dob = new DateTime(1990, 1, 1)
        };

        var profile = new RescuerProfile
        {
            UserId = userId,
            ExperienceYears = 5,
            VehicleType = VehicleType.Motorbike,
            LicensePlate = "ABC-1234",
            CoverageRadiusKM = 15.5,
            IsAvailable = true,
            AvailableUpdatedAt = DateTime.UtcNow
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _rescuerProfileRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(profile);

        // Act
        var result = await _sut.GetRescuerProfileAsync(userId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        result.Data.Should().NotBeNull();

        var dto = result.Data as RescuerProfileDto;
        dto.Should().NotBeNull();
        dto!.UserId.Should().Be(userId);
        dto.FirstName.Should().Be("John");
        dto.LastName.Should().Be("Doe");
        dto.ExperienceYears.Should().Be(5);
        dto.VehicleType.Should().Be(VehicleType.Motorbike);
        dto.IsAvailable.Should().BeTrue();
    }

    #endregion

    #region UpdateRescuerProfileAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: UpdateRescuerProfileAsync with empty user ID
    /// Precondition: userId is Guid.Empty
    /// Expected Result: Returns Auth_Warning0007
    /// </summary>
    [Fact]
    public async Task UpdateRescuerProfileAsync_EmptyUserId_ReturnsAuthWarning()
    {
        // Arrange
        var userId = Guid.Empty;
        var dto = new RescuerProfileDto();

        // Act
        var result = await _sut.UpdateRescuerProfileAsync(userId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0007);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: UpdateRescuerProfileAsync with invalid DTO type
    /// Precondition: DTO is not RescuerProfileDto
    /// Expected Result: Returns SYS_Warning0001 (validation error)
    /// </summary>
    [Fact]
    public async Task UpdateRescuerProfileAsync_InvalidDtoType_ReturnsValidationWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        object invalidDto = new { Name = "Test" }; // Wrong type

        // Act
        var result = await _sut.UpdateRescuerProfileAsync(userId, invalidDto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: UpdateRescuerProfileAsync when user does not exist
    /// Precondition: Valid userId but no User entity
    /// Expected Result: Returns SYS_Warning0004 (not found)
    /// </summary>
    [Fact]
    public async Task UpdateRescuerProfileAsync_UserNotFound_ReturnsNotFoundWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = new RescuerProfileDto
        {
            FirstName = "John",
            LastName = "Doe"
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync((User?)null);

        // Act
        var result = await _sut.UpdateRescuerProfileAsync(userId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: UpdateRescuerProfileAsync when profile does not exist
    /// Precondition: User exists but RescuerProfile missing
    /// Expected Result: Returns SYS_Warning0004 (profile not found)
    /// </summary>
    [Fact]
    public async Task UpdateRescuerProfileAsync_ProfileNotFound_ReturnsNotFoundWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        var dto = new RescuerProfileDto
        {
            FirstName = "John",
            LastName = "Doe"
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _rescuerProfileRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync((RescuerProfile?)null);

        // Act
        var result = await _sut.UpdateRescuerProfileAsync(userId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: UpdateRescuerProfileAsync updates both User and RescuerProfile
    /// Precondition: Valid user and profile exist
    /// Expected Result: Returns SYS_Success0003 with updated data
    /// </summary>
    [Fact]
    public async Task UpdateRescuerProfileAsync_ValidUpdate_UpdatesBothEntities()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            FirstName = "Old First",
            LastName = "Old Last",
            Phone = "+1111111111",
            Avatar = "old-avatar.jpg",
            Address = "Old Address",
            Gender = Gender.Male,
            Dob = new DateTime(1990, 1, 1)
        };

        var profile = new RescuerProfile
        {
            UserId = userId,
            ExperienceYears = 3,
            VehicleType = VehicleType.Car,
            LicensePlate = "OLD-123",
            CoverageRadiusKM = 10.0,
            IsAvailable = false
        };

        var dto = new RescuerProfileDto
        {
            FirstName = "New First",
            LastName = "New Last",
            Phone = "+2222222222",
            Avatar = "new-avatar.jpg",
            Address = "New Address",
            Gender = Gender.Female,
            Dob = new DateTime(1992, 6, 15),
            ExperienceYears = 7,
            VehicleType = VehicleType.Motorbike,
            LicensePlate = "NEW-456",
            CoverageRadiusKM = 20.5,
            IsAvailable = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _rescuerProfileRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(profile);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(2); // User + Profile

        // Act
        var result = await _sut.UpdateRescuerProfileAsync(userId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
        result.Data.Should().NotBeNull();

        // Verify User updates
        user.FirstName.Should().Be("New First");
        user.LastName.Should().Be("New Last");
        user.Phone.Should().Be("+2222222222");
        user.Address.Should().Be("New Address");
        user.Gender.Should().Be(Gender.Female);

        // Verify RescuerProfile updates
        profile.ExperienceYears.Should().Be(7);
        profile.VehicleType.Should().Be(VehicleType.Motorbike);
        profile.LicensePlate.Should().Be("NEW-456");
        profile.CoverageRadiusKM.Should().Be(20.5);
        profile.IsAvailable.Should().BeTrue();

        _userRepoMock.Verify(r => r.UpdateAsync(user), Times.Once);
        _rescuerProfileRepoMock.Verify(r => r.UpdateAsync(profile), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: UpdateRescuerProfileAsync with minimum experience years
    /// Precondition: ExperienceYears = 0
    /// Expected Result: Updates successfully with zero experience
    /// </summary>
    [Fact]
    public async Task UpdateRescuerProfileAsync_ZeroExperience_UpdatesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, FirstName = "John", LastName = "Doe" };
        var profile = new RescuerProfile { UserId = userId, ExperienceYears = 5 };

        var dto = new RescuerProfileDto
        {
            FirstName = "John",
            LastName = "Doe",
            ExperienceYears = 0, // Zero experience (new rescuer)
            VehicleType = VehicleType.Motorbike,
            LicensePlate = "ABC-123",
            CoverageRadiusKM = 10.0,
            IsAvailable = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _rescuerProfileRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(profile);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        var result = await _sut.UpdateRescuerProfileAsync(userId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
        profile.ExperienceYears.Should().Be(0);
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: UpdateRescuerProfileAsync toggles availability status
    /// Precondition: Profile starts with IsAvailable = true
    /// Expected Result: IsAvailable toggled to false and AvailableUpdatedAt updated
    /// </summary>
    [Fact]
    public async Task UpdateRescuerProfileAsync_ToggleAvailability_UpdatesTimestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, FirstName = "John", LastName = "Doe" };
        var oldTimestamp = DateTime.UtcNow.AddHours(-5);
        var profile = new RescuerProfile
        {
            UserId = userId,
            IsAvailable = true,
            AvailableUpdatedAt = oldTimestamp,
            ExperienceYears = 3,
            VehicleType = VehicleType.Car,
            LicensePlate = "ABC-123",
            CoverageRadiusKM = 15.0
        };

        var dto = new RescuerProfileDto
        {
            FirstName = "John",
            LastName = "Doe",
            ExperienceYears = 3,
            VehicleType = VehicleType.Car,
            LicensePlate = "ABC-123",
            CoverageRadiusKM = 15.0,
            IsAvailable = false // Toggled!
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _rescuerProfileRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(profile);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        var result = await _sut.UpdateRescuerProfileAsync(userId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
        profile.IsAvailable.Should().BeFalse();
        profile.AvailableUpdatedAt.Should().BeAfter(oldTimestamp);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: UpdateRescuerProfileAsync with different vehicle types
    /// Precondition: Valid update changing vehicle type
    /// Expected Result: VehicleType updated successfully
    /// </summary>
    [Theory]
    [InlineData(VehicleType.Motorbike)]
    [InlineData(VehicleType.Car)]
    [InlineData(VehicleType.Ambulance)]
    [InlineData(VehicleType.SpecializedVehicle)]
    public async Task UpdateRescuerProfileAsync_DifferentVehicleTypes_UpdatesSuccessfully(VehicleType vehicleType)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, FirstName = "John", LastName = "Doe" };
        var profile = new RescuerProfile
        {
            UserId = userId,
            VehicleType = VehicleType.Car,
            ExperienceYears = 3,
            LicensePlate = "ABC-123",
            CoverageRadiusKM = 10.0,
            IsAvailable = true
        };

        var dto = new RescuerProfileDto
        {
            FirstName = "John",
            LastName = "Doe",
            ExperienceYears = 3,
            VehicleType = vehicleType, // Updated vehicle type
            LicensePlate = "ABC-123",
            CoverageRadiusKM = 10.0,
            IsAvailable = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _rescuerProfileRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(profile);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        var result = await _sut.UpdateRescuerProfileAsync(userId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
        profile.VehicleType.Should().Be(vehicleType);
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: UpdateRescuerProfileAsync with maximum coverage radius
    /// Precondition: CoverageRadiusKM set to large value (100km)
    /// Expected Result: Updates successfully with large radius
    /// </summary>
    [Fact]
    public async Task UpdateRescuerProfileAsync_LargeCoverageRadius_UpdatesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, FirstName = "John", LastName = "Doe" };
        var profile = new RescuerProfile
        {
            UserId = userId,
            CoverageRadiusKM = 10.0,
            ExperienceYears = 3,
            VehicleType = VehicleType.Car,
            LicensePlate = "ABC-123",
            IsAvailable = true
        };

        var dto = new RescuerProfileDto
        {
            FirstName = "John",
            LastName = "Doe",
            ExperienceYears = 3,
            VehicleType = VehicleType.Car,
            LicensePlate = "ABC-123",
            CoverageRadiusKM = 100.0, // Very large radius
            IsAvailable = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _rescuerProfileRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(profile);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        var result = await _sut.UpdateRescuerProfileAsync(userId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
        profile.CoverageRadiusKM.Should().Be(100.0);
    }

    #endregion
}
