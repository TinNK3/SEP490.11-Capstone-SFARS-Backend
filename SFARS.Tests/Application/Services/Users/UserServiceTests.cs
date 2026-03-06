using FluentAssertions;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.User;
using SFARS.Application.Services;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.Interfaces;
using SFARS.Domain.Specifications.Params;
using SFARS.Domain.Specifications.Users;

namespace SFARS.Tests.Application.Services.Users
{
    /// <summary>
    /// Unit tests for UserService:
    /// - GetMeAsync       (GET  /api/me)
    /// - UpdateMeAsync    (PUT  /api/me)
    /// - GetAllUsersAsync (GET  /admin/users)
    ///
    /// Scope: Application service layer only — no EF Core, no real DB.
    /// </summary>
    public class UserServiceTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IGenericRepository<User, Guid>> _userRepoMock;
        private readonly Mock<ISystemMessageService> _msgServiceMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<ILogger<UserService>> _loggerMock;
        private readonly Mock<IPublisher> _publisherMock;
        private readonly Mock<IAdminAuditLogService> _auditLogServiceMock;

        private readonly UserService _sut;

        public UserServiceTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _userRepoMock   = new Mock<IGenericRepository<User, Guid>>();
            _msgServiceMock = new Mock<ISystemMessageService>();
            _mapperMock     = new Mock<IMapper>();
            _loggerMock     = new Mock<ILogger<UserService>>();
            _publisherMock  = new Mock<IPublisher>();
            _auditLogServiceMock = new Mock<IAdminAuditLogService>();

            _unitOfWorkMock
                .Setup(x => x.Repository<User, Guid>())
                .Returns(_userRepoMock.Object);

            _msgServiceMock
                .Setup(x => x.GetMessageAsync(It.IsAny<string>()))
                .ReturnsAsync((string code) => $"Message for {code}");

            _sut = new UserService(
                _msgServiceMock.Object,
                _unitOfWorkMock.Object,
                _mapperMock.Object,
                _loggerMock.Object,
                _publisherMock.Object,
                _auditLogServiceMock.Object
            );
        }

        #region GET /api/me - GetMeAsync Tests

        [Fact]
        public async Task GetMeAsync_UserIdEmpty_ReturnsAuthWarning()
        {
            // Arrange
            var userId = Guid.Empty;

            // Act
            var result = await _sut.GetMeAsync(userId);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0007);
            result.Data.Should().BeNull();
        }

        [Fact]
        public async Task GetMeAsync_UserNotFound_ReturnsNotFoundWarning()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Service calls repository.GetWithSpecAsync(spec)
            _userRepoMock
                .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _sut.GetMeAsync(userId);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
            result.Data.Should().BeNull();
        }

        [Fact]
        public async Task GetMeAsync_ValidUser_ReturnsSuccess_WithRole()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Fake domain user including roles
            var userEntity = new User
            {
                Id = userId,
                UserRoles = new List<UserRole>
                {
                    new UserRole
                    {
                        Role = new Role { RoleName = "User" }
                    }
                }
            };

            var mappedDto = new UserDto
            {
                Id = userId,
                Email = "test@example.com",
                FirstName = "Test",
                LastName = "User",
                Role = "User"
            };

            _userRepoMock
                .Setup(r => r.GetWithSpecAsync(It.IsAny<UserSpecification>(), It.IsAny<bool>()))
                .ReturnsAsync(userEntity);

            _mapperMock
                .Setup(m => m.Map<UserDto>(userEntity))
                .Returns(mappedDto);

            // Act
            var result = await _sut.GetMeAsync(userId);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            result.Data.Should().NotBeNull();
            ((UserDto)result.Data!).Role.Should().Be("User");
        }

        #endregion

        #region PUT /api/me - UpdateMeAsync Tests

        [Fact]
        public async Task UpdateMeAsync_UserIdEmpty_ReturnsAuthWarning()
        {
            // Arrange
            var userId = Guid.Empty;
            var dto = CreateValidUserDto();

            // Act
            var result = await _sut.UpdateMeAsync(userId, dto);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0007);
            result.Data.Should().BeNull();
        }

        [Fact]
        public async Task UpdateMeAsync_UserNotFound_ReturnsNotFoundWarning()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var dto = CreateValidUserDto();

            // Repository.GetByIdAsync returns null => user not found
            _userRepoMock
                .Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _sut.UpdateMeAsync(userId, dto);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
            result.Data.Should().BeNull();
        }

        [Fact]
        public async Task UpdateMeAsync_ValidRequest_UpdatesAndReturnsUserDto_WithRole()
        {
            var userId = Guid.NewGuid();

            // Input DTO (simulate payload from client)
            var dto = CreateValidUserDto();

            // User entity loaded for UPDATE (tracked entity, no roles needed here)
            var existingUser = new User
            {
                Id = userId,
                Email = "test@example.com"
            };

            // User entity RELOADED after update (include role)
            var userWithRole = new User
            {
                Id = userId,
                Email = "test@example.com",
                UserRoles = new List<UserRole>
        {
            new UserRole
            {
                Role = new Role { RoleName = "User" }
            }
        }
            };

            // Expected DTO returned to client
            var expectedDto = new UserDto
            {
                Id = userId,
                Email = "test@example.com",
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Phone = dto.Phone,
                Address = dto.Address,
                Avatar = dto.Avatar,
                Gender = dto.Gender,
                Dob = dto.Dob,
                Role = "User"
            };

            // 1️⃣ Load user for update
            _userRepoMock
                .Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(existingUser);

            // 2️⃣ UpdateAsync called
            _userRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            // 3️⃣ SaveChangesAsync must succeed
            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // 4️⃣ Reload user WITH ROLE after update (CRITICAL FIX)
            _userRepoMock
                .Setup(r => r.GetWithSpecAsync(
                    It.IsAny<UserSpecification>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(userWithRole);

            // 5️⃣ Map reloaded entity → DTO
            _mapperMock
                .Setup(m => m.Map<UserDto>(userWithRole))
                .Returns(expectedDto);

            var result = await _sut.UpdateMeAsync(userId, dto);

            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
            result.Data.Should().NotBeNull();

            var returned = (UserDto)result.Data!;
            returned.FirstName.Should().Be(dto.FirstName);
            returned.LastName.Should().Be(dto.LastName);
            returned.Role.Should().Be("User");

            // Verify pipeline
            _userRepoMock.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Once);
            _userRepoMock.Verify(
                r => r.GetWithSpecAsync(It.IsAny<UserSpecification>(), It.IsAny<bool>()),
                Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region Helpers

        /// <summary>Creates a valid UserDto used as input for update profile tests.</summary>
        private static UserDto CreateValidUserDto() => new UserDto
        {
            FirstName = "Test",
            LastName  = "User",
            Phone     = "0123456789",
            Address   = "HCM City",
            Avatar    = "https://example.com/avatar.png",
            Dob       = new DateTime(2000, 1, 1)
        };

        /// <summary>Returns a minimal empty UserSpecParams (no filters).</summary>
        private static UserSpecParams EmptyParams() => new UserSpecParams();

        /// <summary>Creates a list of fake User entities (no navigation props needed for count/list tests).</summary>
        private static List<User> FakeUsers(int count) =>
            Enumerable.Range(1, count)
                      .Select(i => new User
                      {
                          Id        = Guid.NewGuid(),
                          FirstName = $"User{i}",
                          LastName  = "Test",
                          Email     = $"user{i}@test.com",
                          Status    = UserStatus.Active,
                          UserRoles = new List<UserRole>()
                      })
                      .ToList();

        /// <summary>Creates a list of typed UserDto stubs.</summary>
        private static List<UserDto> FakeDtos(int count) =>
            Enumerable.Range(1, count)
                      .Select(i => new UserDto
                      {
                          Id        = Guid.NewGuid(),
                          FirstName = $"User{i}",
                          LastName  = "Test",
                          Email     = $"user{i}@test.com",
                          Status    = UserStatus.Active,
                          Role      = "User"
                      })
                      .ToList();

        #endregion

        #region GET /admin/users - GetAllUsersAsync Tests

        // ─────────────────────────────────────────────────────────────────────
        // 1. No users found
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsersAsync_WhenNoUsersExist_ReturnsWarning0004_WithEmptyPaginated()
        {
            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(0);

            var result = await _sut.GetAllUsersAsync(EmptyParams(), pageIndex: 0, pageSize: 10);

            result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
            result.Data.Should().NotBeNull();
            var paged = result.Data.Should().BeOfType<PaginatedResultDto<UserDto>>().Subject;
            paged.TotalActualItem.Should().Be(0);
            paged.TotalPage.Should().Be(0);
            paged.Sources.Should().BeEmpty();
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. Happy path — users exist, no filters
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsersAsync_WhenUsersExist_ReturnsSuccess_WithPaginatedDtos()
        {
            var users = FakeUsers(3);
            var dtos  = FakeDtos(3);

            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(3);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(users);

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(users))
                .Returns(dtos);

            var result = await _sut.GetAllUsersAsync(EmptyParams(), pageIndex: 0, pageSize: 10);

            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            var paged = result.Data.Should().BeOfType<PaginatedResultDto<UserDto>>().Subject;
            paged.TotalActualItem.Should().Be(3);
            paged.Sources.Should().HaveCount(3);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. TotalPages — exact multiple (25 items / 5 per page = 5 pages)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsersAsync_TotalPages_ExactMultiple_IsCorrect()
        {
            const int total    = 25;
            const int pageSize = 5;

            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(total);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(FakeUsers(pageSize));

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(It.IsAny<IEnumerable<User>>()))
                .Returns(FakeDtos(pageSize));

            var result = await _sut.GetAllUsersAsync(EmptyParams(), pageIndex: 0, pageSize: pageSize);

            var paged = result.Data.Should().BeOfType<PaginatedResultDto<UserDto>>().Subject;
            paged.TotalPage.Should().Be(5);
            paged.TotalActualItem.Should().Be(total);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4. TotalPages — remainder causes one extra page (11 items / 5 = 3 pages)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsersAsync_TotalPages_WithRemainder_RoundsUp()
        {
            const int total    = 11;
            const int pageSize = 5;

            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(total);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(FakeUsers(pageSize));

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(It.IsAny<IEnumerable<User>>()))
                .Returns(FakeDtos(pageSize));

            var result = await _sut.GetAllUsersAsync(EmptyParams(), pageIndex: 0, pageSize: pageSize);

            var paged = result.Data.Should().BeOfType<PaginatedResultDto<UserDto>>().Subject;
            paged.TotalPage.Should().Be(3); // ceil(11/5) = 3
        }

        // ─────────────────────────────────────────────────────────────────────
        // 5. Guard: pageSize = 0 → defaults to 10, no divide-by-zero
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsersAsync_PageSizeZero_DefaultsToTen_NoException()
        {
            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(3);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(FakeUsers(3));

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(It.IsAny<IEnumerable<User>>()))
                .Returns(FakeDtos(3));

            var act = () => _sut.GetAllUsersAsync(EmptyParams(), pageIndex: 0, pageSize: 0);
            await act.Should().NotThrowAsync();

            var result = await _sut.GetAllUsersAsync(EmptyParams(), pageIndex: 0, pageSize: 0);
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            var paged = result.Data.Should().BeOfType<PaginatedResultDto<UserDto>>().Subject;
            paged.PageSize.Should().Be(10);   // corrected from 0 → 10
            paged.TotalPage.Should().Be(1);   // ceil(3/10)
        }

        // ─────────────────────────────────────────────────────────────────────
        // 6. Guard: pageIndex = -1 → defaults to 0, no negative Skip
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsersAsync_NegativePageIndex_DefaultsToZero_NoException()
        {
            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(3);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(FakeUsers(3));

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(It.IsAny<IEnumerable<User>>()))
                .Returns(FakeDtos(3));

            var act = () => _sut.GetAllUsersAsync(EmptyParams(), pageIndex: -1, pageSize: 10);
            await act.Should().NotThrowAsync();

            var result = await _sut.GetAllUsersAsync(EmptyParams(), pageIndex: -1, pageSize: 10);
            var paged = result.Data.Should().BeOfType<PaginatedResultDto<UserDto>>().Subject;
            paged.PageIndex.Should().Be(0);   // corrected from -1 → 0
        }

        // ─────────────────────────────────────────────────────────────────────
        // 7. Repository methods are called the correct number of times
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsersAsync_WhenUsersExist_CallsCountAndGetAll_ExactlyOnce()
        {
            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(5);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(FakeUsers(5));

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(It.IsAny<IEnumerable<User>>()))
                .Returns(FakeDtos(5));

            await _sut.GetAllUsersAsync(EmptyParams(), pageIndex: 0, pageSize: 10);

            _userRepoMock.Verify(
                r => r.CountAsync(It.IsAny<ISpecification<User>>()),
                Times.Once);

            _userRepoMock.Verify(
                r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()),
                Times.Once);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 8. When no users, GetAllWithSpecAsync is never called (short-circuit)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsersAsync_WhenNoUsers_NeverCallsGetAllWithSpec()
        {
            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(0);

            await _sut.GetAllUsersAsync(EmptyParams(), pageIndex: 0, pageSize: 10);

            _userRepoMock.Verify(
                r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()),
                Times.Never);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 9. Filter by Status — happy path still produces correct result
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsersAsync_WithStatusFilter_Active_ReturnsSuccess()
        {
            var specParams = new UserSpecParams { Status = UserStatus.Active };
            var users = FakeUsers(2);
            var dtos  = FakeDtos(2);

            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(2);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(users);

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(users))
                .Returns(dtos);

            var result = await _sut.GetAllUsersAsync(specParams, pageIndex: 0, pageSize: 10);

            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            var paged = result.Data.Should().BeOfType<PaginatedResultDto<UserDto>>().Subject;
            paged.TotalActualItem.Should().Be(2);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 10. Filter by Role — happy path
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsersAsync_WithRoleFilter_Rescuer_ReturnsSuccess()
        {
            var specParams = new UserSpecParams { Role = "Rescuer" };
            var users = FakeUsers(1);
            var dtos  = FakeDtos(1);

            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(1);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(users);

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(users))
                .Returns(dtos);

            var result = await _sut.GetAllUsersAsync(specParams, pageIndex: 0, pageSize: 10);

            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            var paged = result.Data.Should().BeOfType<PaginatedResultDto<UserDto>>().Subject;
            paged.TotalActualItem.Should().Be(1);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 11. Search term — happy path
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsersAsync_WithSearchTerm_ReturnsMatchingResults()
        {
            var specParams = new UserSpecParams { Search = "nguyen" };
            var users = FakeUsers(2);
            var dtos  = FakeDtos(2);

            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(2);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(users);

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(users))
                .Returns(dtos);

            var result = await _sut.GetAllUsersAsync(specParams, pageIndex: 0, pageSize: 10);

            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            result.Data.Should().NotBeNull();
        }

        // ─────────────────────────────────────────────────────────────────────
        // 12. CreateDateRange filter — both bounds provided
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsersAsync_WithCreateDateRange_BothBounds_ReturnsSuccess()
        {
            var specParams = new UserSpecParams
            {
                CreateDateRange = new DateTime?[] { new DateTime(2025, 1, 1), new DateTime(2025, 12, 31) }
            };
            var users = FakeUsers(4);
            var dtos  = FakeDtos(4);

            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(4);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(users);

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(users))
                .Returns(dtos);

            var result = await _sut.GetAllUsersAsync(specParams, pageIndex: 0, pageSize: 10);

            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            var paged = result.Data.Should().BeOfType<PaginatedResultDto<UserDto>>().Subject;
            paged.TotalActualItem.Should().Be(4);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 13. PaginatedResultDto shape — all fields populated correctly
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsersAsync_PaginatedResultShape_AllFieldsCorrect()
        {
            const int total     = 15;
            const int pageIndex = 1;
            const int pageSize  = 5;

            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(total);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(FakeUsers(pageSize));

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(It.IsAny<IEnumerable<User>>()))
                .Returns(FakeDtos(pageSize));

            var result = await _sut.GetAllUsersAsync(EmptyParams(), pageIndex, pageSize);

            var paged = result.Data.Should().BeOfType<PaginatedResultDto<UserDto>>().Subject;
            paged.PageIndex.Should().Be(pageIndex);
            paged.PageSize.Should().Be(pageSize);
            paged.TotalActualItem.Should().Be(total);
            paged.TotalPage.Should().Be(3);           // ceil(15/5) = 3
            paged.Sources.Should().HaveCount(pageSize);
        }

        // ─────────────────────────────────────────────────────────────────────
        // BOUNDARY TESTS - Pagination Edge Cases
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Test Type: BOUNDARY
        /// Tests: GetAllUsersAsync requesting page beyond last available page
        /// Precondition: Total 10 users, pageSize=10, requesting pageIndex=1 (2nd page)
        /// Expected Result: Returns empty results but no error
        /// </summary>
        [Fact]
        public async Task GetAllUsersAsync_PageIndexBeyondLastPage_ReturnsEmptyResults()
        {
            // Arrange - 10 total users, pageSize 10 means only 1 page (index 0)
            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(10);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<User>()); // No results for page 1

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(It.IsAny<IEnumerable<User>>()))
                .Returns(new List<UserDto>());

            // Act - Request page 1 (2nd page) when only page 0 exists
            var result = await _sut.GetAllUsersAsync(EmptyParams(), pageIndex: 1, pageSize: 10);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002); // Should still be success
            var paged = result.Data.Should().BeOfType<PaginatedResultDto<UserDto>>().Subject;
            paged.Sources.Should().BeEmpty();
            paged.TotalPage.Should().Be(1); // Only 1 page exists
            paged.PageIndex.Should().Be(1); // Requested page
        }

        /// <summary>
        /// Test Type: BOUNDARY
        /// Tests: GetAllUsersAsync with pageSize at exactly total items
        /// Precondition: 20 total users, pageSize=20 (exact match)
        /// Expected Result: Single page with all items, TotalPages=1
        /// </summary>
        [Fact]
        public async Task GetAllUsersAsync_PageSizeExactlyEqualToTotal_ReturnsSinglePage()
        {
            // Arrange
            const int total = 20;
            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(total);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(FakeUsers(total));

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(It.IsAny<IEnumerable<User>>()))
                .Returns(FakeDtos(total));

            // Act - pageSize equals total
            var result = await _sut.GetAllUsersAsync(EmptyParams(), pageIndex: 0, pageSize: total);

            // Assert
            var paged = result.Data.Should().BeOfType<PaginatedResultDto<UserDto>>().Subject;
            paged.TotalPage.Should().Be(1); // BOUNDARY: Exactly 1 page
            paged.Sources.Should().HaveCount(total);
            paged.TotalActualItem.Should().Be(total);
        }

        /// <summary>
        /// Test Type: BOUNDARY
        /// Tests: GetAllUsersAsync with pageSize just one less than total
        /// Precondition: 20 total users, pageSize=19
        /// Expected Result: 2 pages (19 + 1)
        /// </summary>
        [Fact]
        public async Task GetAllUsersAsync_PageSizeOneLessThanTotal_ReturnsTwoPages()
        {
            // Arrange
            const int total = 20;
            const int pageSize = 19;
            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(total);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(FakeUsers(pageSize)); // First page

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(It.IsAny<IEnumerable<User>>()))
                .Returns(FakeDtos(pageSize));

            // Act
            var result = await _sut.GetAllUsersAsync(EmptyParams(), pageIndex: 0, pageSize: pageSize);

            // Assert - ceil(20/19) = 2
            var paged = result.Data.Should().BeOfType<PaginatedResultDto<UserDto>>().Subject;
            paged.TotalPage.Should().Be(2); // BOUNDARY: Forces pagination
            paged.Sources.Should().HaveCount(pageSize);
        }

        /// <summary>
        /// Test Type: BOUNDARY
        /// Tests: GetAllUsersAsync requesting last page with partial results
        /// Precondition: 25 total users, pageSize=10, requesting page 2 (last page)
        /// Expected Result: Returns 5 items (25 % 10 = 5)
        /// </summary>
        [Fact]
        public async Task GetAllUsersAsync_LastPageWithPartialResults_ReturnsRemainingItems()
        {
            // Arrange
            const int total = 25;
            const int pageSize = 10;
            const int lastPageItems = 5; // 25 % 10 = 5

            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(total);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(FakeUsers(lastPageItems)); // Last page has only 5 items

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(It.IsAny<IEnumerable<User>>()))
                .Returns(FakeDtos(lastPageItems));

            // Act - Request page 2 (3rd page, last page)
            var result = await _sut.GetAllUsersAsync(EmptyParams(), pageIndex: 2, pageSize: pageSize);

            // Assert
            var paged = result.Data.Should().BeOfType<PaginatedResultDto<UserDto>>().Subject;
            paged.TotalPage.Should().Be(3); // ceil(25/10) = 3 pages
            paged.PageIndex.Should().Be(2); // Last page index
            paged.Sources.Should().HaveCount(lastPageItems); // BOUNDARY: Partial page
        }

        /// <summary>
        /// Test Type: BOUNDARY
        /// Tests: GetAllUsersAsync with very large pageSize
        /// Precondition: 100 users, pageSize=1000 (exceeds total)
        /// Expected Result: Returns all 100 users in single page
        /// </summary>
        [Fact]
        public async Task GetAllUsersAsync_PageSizeExceedsTotal_ReturnsAllInOnePage()
        {
            // Arrange
            const int total = 100;
            const int excessivePageSize = 1000;

            _userRepoMock
                .Setup(r => r.CountAsync(It.IsAny<ISpecification<User>>()))
                .ReturnsAsync(total);

            _userRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync(FakeUsers(total));

            _mapperMock
                .Setup(m => m.Map<IEnumerable<UserDto>>(It.IsAny<IEnumerable<User>>()))
                .Returns(FakeDtos(total));

            // Act
            var result = await _sut.GetAllUsersAsync(EmptyParams(), pageIndex: 0, pageSize: excessivePageSize);

            // Assert
            var paged = result.Data.Should().BeOfType<PaginatedResultDto<UserDto>>().Subject;
            paged.TotalPage.Should().Be(1); // Only 1 page needed
            paged.Sources.Should().HaveCount(total); // All items returned
        }

        #endregion

        #region POST /admin/users - CreateUserAsync Tests

        [Fact]
        public async Task CreateUserAsync_EmailAlreadyTaken_ReturnsAuthWarning0006()
        {
            _userRepoMock
                .Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
                .ReturnsAsync(true);

            var result = await _sut.CreateUserAsync(Guid.NewGuid(), new UserDto
            {
                FirstName = "John", LastName = "Doe",
                Email = "existing@test.com", Password = "Pass123!",
                Role = RoleType.User.ToString()
            });

            result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0006);
        }

        [Fact]
        public async Task CreateUserAsync_RoleNotFoundInDb_ReturnsWarning0004()
        {
            _userRepoMock
                .Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
                .ReturnsAsync(false);

            var roleRepoMock = new Mock<IGenericRepository<Role, Guid>>();
            roleRepoMock
                .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Role>>(), It.IsAny<bool>()))
                .ReturnsAsync((Role?)null);

            _unitOfWorkMock
                .Setup(u => u.Repository<Role, Guid>())
                .Returns(roleRepoMock.Object);

            var result = await _sut.CreateUserAsync(Guid.NewGuid(), new UserDto
            {
                FirstName = "John", LastName = "Doe",
                Email = "new@test.com", Password = "Pass123!",
                Role = RoleType.Rescuer.ToString()
            });

            result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        }

        [Theory]
        [InlineData(RoleType.User)]
        [InlineData(RoleType.Rescuer)]
        [InlineData(RoleType.Admin)]
        public async Task CreateUserAsync_HappyPath_CreatesUser_ReturnsSuccess0001(RoleType roleType)
        {
            var role = new Role { Id = Guid.NewGuid(), RoleName = roleType.ToString() };

            _userRepoMock
                .Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
                .ReturnsAsync(false);

            _userRepoMock
                .Setup(r => r.AddAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            var roleRepoMock     = new Mock<IGenericRepository<Role, Guid>>();
            var userRoleRepoMock = new Mock<IGenericRepository<UserRole, Guid>>();

            roleRepoMock
                .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Role>>(), It.IsAny<bool>()))
                .ReturnsAsync(role);

            userRoleRepoMock
                .Setup(r => r.AddAsync(It.IsAny<UserRole>()))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(u => u.Repository<Role, Guid>()).Returns(roleRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Repository<UserRole, Guid>()).Returns(userRoleRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.SaveChangesWithTransactionAsync()).ReturnsAsync(1);

            _mapperMock
                .Setup(m => m.Map<UserDto>(It.IsAny<User>()))
                .Returns(new UserDto { Email = "new@test.com", FirstName = "John" });

            var result = await _sut.CreateUserAsync(Guid.NewGuid(), new UserDto
            {
                FirstName = "John", LastName = "Doe",
                Email = "new@test.com", Password = "Pass123!",
                Role = roleType.ToString()
            });

            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
            result.Data.Should().NotBeNull();
            ((UserDto)result.Data!).Role.Should().Be(roleType.ToString());

            _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
            userRoleRepoMock.Verify(r => r.AddAsync(It.IsAny<UserRole>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesWithTransactionAsync(), Times.Once);
        }

        #endregion

        #region PUT /admin/users/{id}/status - UpdateUserStatusAsync Tests

        [Fact]
        public async Task UpdateUserStatusAsync_TargetIdEmpty_ReturnsAuthWarning()
        {
            var result = await _sut.UpdateUserStatusAsync(Guid.NewGuid(), Guid.Empty, UserStatus.Banned, null);

            result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0007);
            result.Data.Should().BeNull();
        }

        [Fact]
        public async Task UpdateUserStatusAsync_UserNotFound_ReturnsAdminWarning0001()
        {
            var adminId  = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _userRepoMock
                .Setup(r => r.GetByIdAsync(targetId))
                .ReturnsAsync((User?)null);

            var result = await _sut.UpdateUserStatusAsync(adminId, targetId, UserStatus.Banned, null);

            result.ResultCode.Should().Be(ResultCodeConst.Admin_Warning0001);
        }

        [Fact]
        public async Task UpdateUserStatusAsync_SelfModify_ReturnsAdminWarning0002()
        {
            var adminId = Guid.NewGuid(); // same as targetId

            _userRepoMock
                .Setup(r => r.GetByIdAsync(adminId))
                .ReturnsAsync(new User { Id = adminId, Status = UserStatus.Active });

            var result = await _sut.UpdateUserStatusAsync(adminId, adminId, UserStatus.Banned, null);

            result.ResultCode.Should().Be(ResultCodeConst.Admin_Warning0002);
        }

        [Theory]
        [InlineData(UserStatus.Active)]
        [InlineData(UserStatus.Inactive)]
        [InlineData(UserStatus.Banned)]
        [InlineData(UserStatus.Deleted)]
        public async Task UpdateUserStatusAsync_HappyPath_UpdatesStatus_ReturnsAdminSuccess0002(UserStatus newStatus)
        {
            var adminId  = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            var user = new User { Id = targetId, Status = UserStatus.Active };

            _userRepoMock
                .Setup(r => r.GetByIdAsync(targetId))
                .ReturnsAsync(user);

            _userRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            var result = await _sut.UpdateUserStatusAsync(adminId, targetId, newStatus, "test reason");

            result.ResultCode.Should().Be(ResultCodeConst.Admin_Success0002);
            user.Status.Should().Be(newStatus);
            _userRepoMock.Verify(r => r.UpdateAsync(user), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region GET /admin/users/{id} - GetUserByIdAsync Tests

        [Fact]
        public async Task GetUserByIdAsync_UserIdEmpty_ReturnsAuthWarning()
        {
            var result = await _sut.GetUserByIdAsync(Guid.Empty);

            result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0007);
            result.Data.Should().BeNull();
        }

        [Fact]
        public async Task GetUserByIdAsync_UserNotFound_ReturnsWarning0004()
        {
            var userId = Guid.NewGuid();

            _userRepoMock
                .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<User>>(), It.IsAny<bool>()))
                .ReturnsAsync((User?)null);

            var result = await _sut.GetUserByIdAsync(userId);

            result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
            result.Data.Should().BeNull();
        }

        [Fact]
        public async Task GetUserByIdAsync_UserFound_ReturnsSuccess_WithDto()
        {
            var userId = Guid.NewGuid();

            var userEntity = new User
            {
                Id        = userId,
                Email     = "admin@test.com",
                FirstName = "John",
                LastName  = "Doe",
                UserRoles = new List<UserRole>
                {
                    new UserRole { Role = new Role { RoleName = "Rescuer" } }
                }
            };

            var expectedDto = new UserDto
            {
                Id        = userId,
                Email     = "admin@test.com",
                FirstName = "John",
                LastName  = "Doe",
                Role      = "Rescuer"
            };

            _userRepoMock
                .Setup(r => r.GetWithSpecAsync(It.IsAny<UserSpecification>(), It.IsAny<bool>()))
                .ReturnsAsync(userEntity);

            _mapperMock
                .Setup(m => m.Map<UserDto>(userEntity))
                .Returns(expectedDto);

            var result = await _sut.GetUserByIdAsync(userId);

            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            result.Data.Should().NotBeNull();
            var dto = (UserDto)result.Data!;
            dto.Id.Should().Be(userId);
            dto.Role.Should().Be("Rescuer");
        }

        #endregion
    }
}