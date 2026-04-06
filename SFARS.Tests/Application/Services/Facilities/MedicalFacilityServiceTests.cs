using FluentAssertions;
using MapsterMapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NetTopologySuite.Geometries;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Facility;
using SFARS.Application.Services;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Interfaces;

namespace SFARS.Tests.Application.Services.Facilities
{
    /// <summary>
    /// Unit tests for MedicalFacilityService:
    /// - GetFacilityByIdAsync    (GET  /admin/facilities/{id})
    /// - CreateFacilityAsync     (POST /admin/facilities)
    /// - UpdateFacilityAsync     (PUT  /admin/facilities/{id})
    /// - UpdateAntivenomAsync    (PUT  /admin/facilities/{id}/antivenom)
    /// - DeactivateFacilityAsync (PUT  /admin/facilities/{id}/deactivate)
    /// - ActivateFacilityAsync   (PUT  /admin/facilities/{id}/activate)
    ///
    /// Scope: Application service layer only — no EF Core, no real DB.
    /// </summary>
    public class MedicalFacilityServiceTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IGenericRepository<MedicalFacility, Guid>> _facilityRepoMock;
        private readonly Mock<IGenericRepository<User, Guid>> _userRepoMock;
        private readonly Mock<ISystemMessageService> _msgServiceMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<ILogger<MedicalFacilityService>> _loggerMock;
        private readonly IMemoryCache _memoryCache;

        private readonly MedicalFacilityService _sut;

        public MedicalFacilityServiceTests()
        {
            _unitOfWorkMock   = new Mock<IUnitOfWork>();
            _facilityRepoMock = new Mock<IGenericRepository<MedicalFacility, Guid>>();
            _userRepoMock     = new Mock<IGenericRepository<User, Guid>>();
            _msgServiceMock   = new Mock<ISystemMessageService>();
            _mapperMock       = new Mock<IMapper>();
            _loggerMock       = new Mock<ILogger<MedicalFacilityService>>();
            _memoryCache      = new MemoryCache(new MemoryCacheOptions());

            _unitOfWorkMock
                .Setup(x => x.Repository<MedicalFacility, Guid>())
                .Returns(_facilityRepoMock.Object);

            _unitOfWorkMock
                .Setup(x => x.Repository<User, Guid>())
                .Returns(_userRepoMock.Object);

            _msgServiceMock
                .Setup(x => x.GetMessageAsync(It.IsAny<string>()))
                .ReturnsAsync((string code) => $"Message for {code}");

            _mapperMock
                .Setup(m => m.Map<MedicalFacility>(It.IsAny<FacilityDto>()))
                .Returns((FacilityDto dto) => new MedicalFacility 
                { 
                    Id = dto.Id != Guid.Empty ? dto.Id : Guid.NewGuid(),
                    Name = dto.Name, 
                    Type = dto.FacilityType,
                    HasAntivenom = dto.HasAntivenom
                });

            _sut = new MedicalFacilityService(
                _msgServiceMock.Object,
                _unitOfWorkMock.Object,
                _mapperMock.Object,
                _memoryCache,
                _loggerMock.Object
            );
        }

        #region GET /admin/facilities/{id} — GetFacilityByIdAsync

        /// <summary>
        /// Test Type: ABNORMAL
        /// Tests: GetFacilityByIdAsync when facility does not exist in database
        /// Precondition: Valid GUID provided, but no matching facility in repository
        /// Expected Result: Returns warning code SYS_Warning0002 with null data
        /// </summary>
        [Fact]
        public async Task GetFacilityByIdAsync_FacilityNotFound_ReturnsNotFoundWarning()
        {
            // Arrange
            var id = Guid.NewGuid();
            _facilityRepoMock
                .Setup(r => r.GetByIdAsync(id))
                .ReturnsAsync((MedicalFacility?)null);

            // Act
            var result = await _sut.GetFacilityByIdAsync(id);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
            result.Data.Should().BeNull();
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: GetFacilityByIdAsync with existing facility
        /// Precondition: Facility exists in database with valid coordinates
        /// Expected Result: Returns success with FacilityDto containing correct coordinates
        /// </summary>
        [Fact]
        public async Task GetFacilityByIdAsync_FacilityExists_ReturnsSuccess_WithDetailDto()
        {
            // Arrange
            var facility = CreateFacility();
            _facilityRepoMock
                .Setup(r => r.GetByIdAsync(facility.Id))
                .ReturnsAsync(facility);

            // Act
            var result = await _sut.GetFacilityByIdAsync(facility.Id);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.Medical_Success0001);
            result.Data.Should().NotBeNull();
            var dto = (FacilityDto)result.Data!;
            dto.Name.Should().Be(facility.Name);
            dto.Latitude.Should().Be(facility.Location.Y);
            dto.Longitude.Should().Be(facility.Location.X);
        }

        #endregion

        #region POST /admin/facilities — CreateFacilityAsync

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: CreateFacilityAsync with all valid required fields
        /// Precondition: Database accessible, valid facility data within normal ranges
        /// Expected Result: Success code Medical_Success0002, facility created with IsActive=true
        /// </summary>
        [Fact]
        public async Task CreateFacilityAsync_ValidRequest_ReturnsSuccess_WithDetailDto()
        {
            // Arrange
            var request = CreateValidRequest();

            _facilityRepoMock
                .Setup(r => r.AddAsync(It.IsAny<MedicalFacility>()))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act
            var result = await _sut.CreateFacilityAsync(request);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.Medical_Success0002);
            result.Data.Should().NotBeNull();
            var dto = (FacilityDto)result.Data!;
            dto.Name.Should().Be(request.Name);
            dto.IsActive.Should().BeTrue();

            _facilityRepoMock.Verify(r => r.AddAsync(It.IsAny<MedicalFacility>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: CreateFacilityAsync with antivenom flag set to true
        /// Precondition: Valid facility data with HasAntivenom=true
        /// Expected Result: Success with AntivenomUpdatedAt timestamp set
        /// </summary>
        [Fact]
        public async Task CreateFacilityAsync_WithAntivenom_SetsAntivenomUpdatedAt()
        {
            // Arrange
            var request = CreateValidRequest();
            request.HasAntivenom = true;

            _facilityRepoMock
                .Setup(r => r.AddAsync(It.IsAny<MedicalFacility>()))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act
            var result = await _sut.CreateFacilityAsync(request);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.Medical_Success0002);
            var dto = (FacilityDto)result.Data!;
            dto.HasAntivenom.Should().BeTrue();
            dto.AntivenomUpdatedAt.Should().NotBeNull();
        }

        /// <summary>
        /// Test Type: ABNORMAL
        /// Tests: CreateFacilityAsync when database save operation fails
        /// Precondition: Valid request but SaveChangesAsync returns 0 (no rows affected)
        /// Expected Result: Returns system failure code SYS_Fail0001
        /// </summary>
        #endregion

        #region PUT /admin/facilities/{id} — UpdateFacilityAsync

        /// <summary>
        /// Test Type: ABNORMAL
        /// Tests: UpdateFacilityAsync when target facility does not exist
        /// Precondition: Valid GUID and request, but facility not found in database
        /// Expected Result: Returns not found warning SYS_Warning0002
        /// </summary>
        [Fact]
        public async Task UpdateFacilityAsync_FacilityNotFound_ReturnsNotFoundWarning()
        {
            // Arrange
            var id = Guid.NewGuid();
            var request = new FacilityDto
            {
                Name = "Updated", FacilityType = FacilityType.Hospital,
                Latitude = 10.8, Longitude = 106.7
            };

            _facilityRepoMock
                .Setup(r => r.GetByIdAsync(id))
                .ReturnsAsync((MedicalFacility?)null);

            // Act
            var result = await _sut.UpdateFacilityAsync(id, request);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: UpdateFacilityAsync with valid data changes
        /// Precondition: Facility exists, valid update request with new values
        /// Expected Result: Success with updated DTO reflecting all changes
        /// </summary>
        [Fact]
        public async Task UpdateFacilityAsync_ValidRequest_ReturnsSuccess_WithUpdatedDto()
        {
            // Arrange
            var facility = CreateFacility();
            var request = new FacilityDto
            {
                Name = "Updated Hospital",
                FacilityType = FacilityType.Clinic,
                Latitude = 11.0, Longitude = 107.0,
                PhoneNumber = "0901234567"
            };

            _facilityRepoMock
                .Setup(r => r.GetByIdAsync(facility.Id))
                .ReturnsAsync(facility);

            _facilityRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<MedicalFacility>()))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act
            var result = await _sut.UpdateFacilityAsync(facility.Id, request);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.Medical_Success0003);
            var dto = (FacilityDto)result.Data!;
            dto.Name.Should().Be("Updated Hospital");

            _facilityRepoMock.Verify(r => r.UpdateAsync(It.IsAny<MedicalFacility>()), Times.Once);
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: UpdateFacilityAsync when antivenom status changes from false to true
        /// Precondition: Facility exists with HasAntivenom=false, request sets it to true
        /// Expected Result: Success with AntivenomUpdatedAt timestamp updated
        /// </summary>
        [Fact]
        public async Task UpdateFacilityAsync_AntivenomChanged_UpdatesAntivenomTimestamp()
        {
            // Arrange
            var facility = CreateFacility();
            facility.HasAntivenom = false;
            facility.AntivenomUpdatedAt = null;

            var request = new FacilityDto
            {
                Name = facility.Name, FacilityType = facility.Type,
                Latitude = 10.8, Longitude = 106.7,
                HasAntivenom = true // changed from false → true
            };

            _facilityRepoMock.Setup(r => r.GetByIdAsync(facility.Id)).ReturnsAsync(facility);
            _facilityRepoMock.Setup(r => r.UpdateAsync(It.IsAny<MedicalFacility>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _sut.UpdateFacilityAsync(facility.Id, request);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.Medical_Success0003);
            facility.HasAntivenom.Should().BeTrue();
            facility.AntivenomUpdatedAt.Should().NotBeNull();
        }

        /// <summary>
        /// Test Type: ABNORMAL
        /// Tests: UpdateFacilityAsync when SaveChanges fails
        /// Precondition: Valid update but database save returns 0 rows affected
        /// Expected Result: Returns system failure code SYS_Fail0001
        /// </summary>
        [Fact]
        public async Task UpdateFacilityAsync_SaveFails_ReturnsSysFail()
        {
            // Arrange
            var facility = CreateFacility();
            var request = new FacilityDto
            {
                Name = "Updated", FacilityType = FacilityType.Hospital,
                Latitude = 10.8, Longitude = 106.7
            };

            _facilityRepoMock.Setup(r => r.GetByIdAsync(facility.Id)).ReturnsAsync(facility);
            _facilityRepoMock.Setup(r => r.UpdateAsync(It.IsAny<MedicalFacility>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(0); // Fail

            // Act
            var result = await _sut.UpdateFacilityAsync(facility.Id, request);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Fail0003);
        }

        #endregion

        #region PUT /admin/facilities/{id}/antivenom — UpdateAntivenomAsync

        /// <summary>
        /// Test Type: ABNORMAL
        /// Tests: UpdateAntivenomAsync when facility does not exist
        /// Precondition: Valid GUID but no matching facility in database
        /// Expected Result: Returns not found warning SYS_Warning0002
        /// </summary>
        [Fact]
        public async Task UpdateAntivenomAsync_FacilityNotFound_ReturnsNotFoundWarning()
        {
            _facilityRepoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((MedicalFacility?)null);

            var result = await _sut.UpdateAntivenomAsync(Guid.NewGuid(), true);

            result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: UpdateAntivenomAsync with valid status change
        /// Precondition: Facility exists with HasAntivenom=false
        /// Expected Result: Success with antivenom status and timestamp updated
        /// </summary>
        [Fact]
        public async Task UpdateAntivenomAsync_ValidRequest_UpdatesStatusAndTimestamp()
        {
            // Arrange
            var facility = CreateFacility();
            facility.HasAntivenom = false;

            _facilityRepoMock.Setup(r => r.GetByIdAsync(facility.Id)).ReturnsAsync(facility);
            _facilityRepoMock.Setup(r => r.UpdateAsync(It.IsAny<MedicalFacility>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _sut.UpdateAntivenomAsync(facility.Id, true);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.Medical_Success0004);
            facility.HasAntivenom.Should().BeTrue();
            facility.AntivenomUpdatedAt.Should().NotBeNull();
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: UpdateAntivenomAsync setting status to false
        /// Precondition: Facility exists with HasAntivenom=true
        /// Expected Result: Success with antivenom disabled and timestamp updated
        /// </summary>
        [Fact]
        public async Task UpdateAntivenomAsync_SetToFalse_UpdatesStatusAndTimestamp()
        {
            // Arrange
            var facility = CreateFacility();
            facility.HasAntivenom = true;

            _facilityRepoMock.Setup(r => r.GetByIdAsync(facility.Id)).ReturnsAsync(facility);
            _facilityRepoMock.Setup(r => r.UpdateAsync(It.IsAny<MedicalFacility>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _sut.UpdateAntivenomAsync(facility.Id, false);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.Medical_Success0004);
            facility.HasAntivenom.Should().BeFalse();
            facility.AntivenomUpdatedAt.Should().NotBeNull();
        }

        #endregion

        #region PUT /admin/facilities/{id}/deactivate — DeactivateFacilityAsync

        /// <summary>
        /// Test Type: ABNORMAL
        /// Tests: DeactivateFacilityAsync when facility does not exist
        /// Precondition: Valid GUID but no matching facility in database
        /// Expected Result: Returns not found warning SYS_Warning0002
        /// </summary>
        [Fact]
        public async Task DeactivateFacilityAsync_FacilityNotFound_ReturnsNotFoundWarning()
        {
            _facilityRepoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((MedicalFacility?)null);

            var result = await _sut.DeactivateFacilityAsync(Guid.NewGuid());

            result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
        }

        /// <summary>
        /// Test Type: ABNORMAL
        /// Tests: DeactivateFacilityAsync when facility is already deactivated
        /// Precondition: Facility exists with IsActive=false
        /// Expected Result: Returns business warning Medical_Warning0003 (already deactivated)
        /// </summary>
        [Fact]
        public async Task DeactivateFacilityAsync_AlreadyDeactivated_ReturnsWarning()
        {
            // Arrange
            var facility = CreateFacility();
            facility.IsActive = false;

            _facilityRepoMock.Setup(r => r.GetByIdAsync(facility.Id)).ReturnsAsync(facility);

            // Act
            var result = await _sut.DeactivateFacilityAsync(facility.Id);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.Medical_Warning0003);
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: DeactivateFacilityAsync with active facility
        /// Precondition: Facility exists with IsActive=true
        /// Expected Result: Success with IsActive set to false
        /// </summary>
        [Fact]
        public async Task DeactivateFacilityAsync_ActiveFacility_SetsIsActiveFalse_ReturnsSuccess()
        {
            // Arrange
            var facility = CreateFacility();
            facility.IsActive = true;

            _facilityRepoMock.Setup(r => r.GetByIdAsync(facility.Id)).ReturnsAsync(facility);
            _facilityRepoMock.Setup(r => r.UpdateAsync(It.IsAny<MedicalFacility>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _sut.DeactivateFacilityAsync(facility.Id);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.Medical_Success0005);
            facility.IsActive.Should().BeFalse();
        }

        #endregion

        #region PUT /admin/facilities/{id}/activate — ActivateFacilityAsync

        /// <summary>
        /// Test Type: ABNORMAL
        /// Tests: ActivateFacilityAsync when facility does not exist
        /// Precondition: Valid GUID but no matching facility in database
        /// Expected Result: Returns not found warning SYS_Warning0002
        /// </summary>
        [Fact]
        public async Task ActivateFacilityAsync_FacilityNotFound_ReturnsNotFoundWarning()
        {
            _facilityRepoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((MedicalFacility?)null);

            var result = await _sut.ActivateFacilityAsync(Guid.NewGuid());

            result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
        }

        /// <summary>
        /// Test Type: ABNORMAL
        /// Tests: ActivateFacilityAsync when facility is already active
        /// Precondition: Facility exists with IsActive=true
        /// Expected Result: Returns business warning Medical_Warning0004 (already active)
        /// </summary>
        [Fact]
        public async Task ActivateFacilityAsync_AlreadyActive_ReturnsWarning()
        {
            var facility = CreateFacility();
            facility.IsActive = true;

            _facilityRepoMock.Setup(r => r.GetByIdAsync(facility.Id)).ReturnsAsync(facility);

            var result = await _sut.ActivateFacilityAsync(facility.Id);

            result.ResultCode.Should().Be(ResultCodeConst.Medical_Warning0004);
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: ActivateFacilityAsync with deactivated facility
        /// Precondition: Facility exists with IsActive=false
        /// Expected Result: Success with IsActive set to true
        /// </summary>
        [Fact]
        public async Task ActivateFacilityAsync_DeactivatedFacility_SetsIsActiveTrue_ReturnsSuccess()
        {
            // Arrange
            var facility = CreateFacility();
            facility.IsActive = false;

            _facilityRepoMock.Setup(r => r.GetByIdAsync(facility.Id)).ReturnsAsync(facility);
            _facilityRepoMock.Setup(r => r.UpdateAsync(It.IsAny<MedicalFacility>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _sut.ActivateFacilityAsync(facility.Id);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.Medical_Success0006);
            facility.IsActive.Should().BeTrue();
        }

        #endregion

        #region DELETE /admin/facilities/{id} — DeleteFacilityAsync

        /// <summary>
        /// Test Type: ABNORMAL
        /// Tests: DeleteFacilityAsync when facility does not exist
        /// Precondition: Valid GUID provided, but no matching facility in repository
        /// Expected Result: Returns not found warning SYS_Warning0002 and does not call delete
        /// </summary>
        [Fact]
        public async Task DeleteFacilityAsync_FacilityNotFound_ReturnsNotFoundWarning_AndDoesNotDelete()
        {
            // Arrange
            var id = Guid.NewGuid();

            _facilityRepoMock
                .Setup(r => r.GetByIdAsync(id))
                .ReturnsAsync((MedicalFacility?)null);

            // Act
            var result = await _sut.DeleteFacilityAsync(id);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
            _facilityRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: DeleteFacilityAsync with existing facility and successful delete
        /// Precondition: Facility exists, repository delete affects 1 row, cache contains active facilities
        /// Expected Result: Returns SYS_Success0004 and clears facilities cache
        /// </summary>
        [Fact]
        public async Task DeleteFacilityAsync_ValidFacility_DeleteSuccess_ReturnsSuccess_AndBustsCache()
        {
            // Arrange
            var facility = CreateFacility();
            _memoryCache.Set(LocationConstants.MemCacheFacilitiesActiveKey, new List<MedicalFacility> { facility });

            _facilityRepoMock
                .Setup(r => r.GetByIdAsync(facility.Id))
                .ReturnsAsync(facility);

            _facilityRepoMock
                .Setup(r => r.DeleteAsync(facility.Id))
                .ReturnsAsync(1);

            // Act
            var result = await _sut.DeleteFacilityAsync(facility.Id);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0004);
            _facilityRepoMock.Verify(r => r.DeleteAsync(facility.Id), Times.Once);

            _memoryCache.TryGetValue(LocationConstants.MemCacheFacilitiesActiveKey, out _)
                .Should().BeFalse();
        }

        /// <summary>
        /// Test Type: ABNORMAL
        /// Tests: DeleteFacilityAsync when delete affects 0 rows after pre-check
        /// Precondition: Facility exists in pre-check, but repository delete returns 0 rows
        /// Expected Result: Returns warning SYS_Warning0002 and does not clear facilities cache
        /// </summary>
        [Fact]
        public async Task DeleteFacilityAsync_DeleteAffectsZeroRows_ReturnsWarning_AndKeepsCache()
        {
            // Arrange
            var facility = CreateFacility();
            _memoryCache.Set(LocationConstants.MemCacheFacilitiesActiveKey, new List<MedicalFacility> { facility });

            _facilityRepoMock
                .Setup(r => r.GetByIdAsync(facility.Id))
                .ReturnsAsync(facility);

            _facilityRepoMock
                .Setup(r => r.DeleteAsync(facility.Id))
                .ReturnsAsync(0);

            // Act
            var result = await _sut.DeleteFacilityAsync(facility.Id);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
            _facilityRepoMock.Verify(r => r.DeleteAsync(facility.Id), Times.Once);

            _memoryCache.TryGetValue(LocationConstants.MemCacheFacilitiesActiveKey, out _)
                .Should().BeTrue();
        }

        #endregion

        #region GET /api/facilities/nearby — GetNearbyFacilitiesAsync

        [Fact]
        public async Task GetNearbyFacilitiesAsync_MissingLocationCoords_AndUserHasNoLocation_ReturnsWarning()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User { Id = userId, CurrentLocation = null };

            _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            // Act
            var result = await _sut.GetNearbyFacilitiesAsync(userId, null, null);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.Medical_Warning0006);
        }

        [Fact]
        public async Task GetNearbyFacilitiesAsync_MissingLocationCoords_AndUserHasLocation_InjectsUserLocationAndReturnsSuccess()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User { Id = userId, CurrentLocation = new Point(106.7, 10.8) { SRID = 4326 } };

            var facility = CreateFacility();
            facility.Location = new Point(106.7, 10.8) { SRID = 4326 };

            _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            // Mock active facilities cache via repo
            _facilityRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<MedicalFacility>>(), false))
                .ReturnsAsync(new List<MedicalFacility> { facility });

            // Act
            var result = await _sut.GetNearbyFacilitiesAsync(userId, null, null);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            result.Data.Should().NotBeNull();
            var dtos = (IEnumerable<FacilityDto>)result.Data!;
            dtos.Should().NotBeEmpty();
        }

        #endregion

        #region Helpers

        /// <summary>Creates a test MedicalFacility with valid NTS Point.</summary>
        private static MedicalFacility CreateFacility() => new MedicalFacility
        {
            Id = Guid.NewGuid(),
            Name = "Test Hospital",
            Type = FacilityType.Hospital,
            Address = "123 Test St, HCM City",
            Location = new Point(106.7, 10.8) { SRID = 4326 }, // lon, lat
            PhoneNumber = "0901234567",
            OpenHours = new TimeOnly(8, 0),
            CloseHours = new TimeOnly(17, 0),
            HasAntivenom = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        /// <summary>Creates a valid FacilityDto for testing.</summary>
        private static FacilityDto CreateValidRequest() => new FacilityDto
        {
            Name = "New Hospital",
            FacilityType = FacilityType.Hospital,
            Latitude = 10.8,
            Longitude = 106.7,
            Address = "456 Test Ave, HCM City",
            PhoneNumber = "0912345678",
            OpenHours = new TimeOnly(0, 0),
            CloseHours = new TimeOnly(23, 59),
            EmergencyAvailable = true,
            HasAntivenom = false
        };

        #endregion
    }
}
