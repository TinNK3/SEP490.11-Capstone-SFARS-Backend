using FluentAssertions;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Dtos.User;
using SFARS.Application.Services;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Interfaces;
using SFARS.Domain.Specifications.Users;

namespace SFARS.Tests.Application.Services.Users
{
    /// <summary>
    /// Unit tests for UserService:
    /// - GetMeAsync (GET /api/me)
    /// - UpdateMeAsync (PUT /api/me)
    ///
    /// Scope:
    /// We only test the Application Service behavior (business flow + result codes),
    /// not EF Core internals or real database behaviors.
    /// </summary>
    public class UserServiceTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IGenericRepository<User, Guid>> _userRepoMock;
        private readonly Mock<ISystemMessageService> _msgServiceMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<ILogger<UserService>> _loggerMock;

        private readonly UserService _sut;

        public UserServiceTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _userRepoMock = new Mock<IGenericRepository<User, Guid>>();
            _msgServiceMock = new Mock<ISystemMessageService>();
            _mapperMock = new Mock<IMapper>();
            _loggerMock = new Mock<ILogger<UserService>>();

            // Important: UnitOfWork.Repository<User, Guid>() must return our mocked repository.
            _unitOfWorkMock
                .Setup(x => x.Repository<User, Guid>())
                .Returns(_userRepoMock.Object);

            // Default behavior for message service to avoid null/empty message noise.
            _msgServiceMock
                .Setup(x => x.GetMessageAsync(It.IsAny<string>()))
                .ReturnsAsync((string code) => $"Message for {code}");

            _sut = new UserService(
                _msgServiceMock.Object,
                _unitOfWorkMock.Object,
                _mapperMock.Object,
                _loggerMock.Object
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
                .Setup(r => r.GetWithSpecAsync(It.IsAny<UserByIdWithRoleSpecification>(), It.IsAny<bool>()))
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
            // Arrange
            var userId = Guid.NewGuid();

            // DTO input (simulate client payload mapped from request)
            // NOTE: In your service, you manually assign allowed fields.
            var dto = CreateValidUserDto();

            // Existing user in DB
            var userEntity = new User
            {
                Id = userId,
                Email = "test@example.com"
            };

            // After update, you want response includes Role.
            // The most reliable way is to re-load user WITH ROLE before mapping (your "cách 2"),
            // or map from a userWithRole loaded by spec. In test we simulate final returned mapping.
            var userDtoWithRole = new UserDto
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

            // 1) Load entity for update
            _userRepoMock
                .Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(userEntity);

            // 2) UpdateAsync called
            _userRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            // 3) SaveChangesAsync must report success (>0) for success flow
            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // 4) After saving, service may map from user entity or from a reloaded entity
            // We'll assume service returns mapping result with Role filled.
            _mapperMock
                .Setup(m => m.Map<UserDto>(It.IsAny<User>()))
                .Returns(userDtoWithRole);

            // Act
            var result = await _sut.UpdateMeAsync(userId, dto);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
            result.Data.Should().NotBeNull();

            var returned = (UserDto)result.Data!;
            returned.FirstName.Should().Be(dto.FirstName);
            returned.Role.Should().Be("User");

            // Verify that update pipeline actually happened
            _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Creates a valid UserDto used as input for update profile tests.
        /// Keep it minimal but valid according to your UpdateProfileRequestValidator.
        /// </summary>
        private static UserDto CreateValidUserDto()
        {
            return new UserDto
            {
                FirstName = "Test",
                LastName = "User",
                Phone = "0123456789",
                Address = "HCM City",
                Avatar = "https://example.com/avatar.png",
                // Gender and Dob are optional in your validator (Dob must be in the past if provided)
                Dob = new DateTime(2000, 1, 1)
            };
        }

        #endregion
    }
}