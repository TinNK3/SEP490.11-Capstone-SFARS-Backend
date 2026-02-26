using FluentAssertions;
using MapsterMapper;
using MediatR;
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
        private readonly Mock<IPublisher> _publisherMock;

        private readonly UserService _sut;

        public UserServiceTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _userRepoMock = new Mock<IGenericRepository<User, Guid>>();
            _msgServiceMock = new Mock<ISystemMessageService>();
            _mapperMock = new Mock<IMapper>();
            _loggerMock = new Mock<ILogger<UserService>>();
            _publisherMock = new Mock<IPublisher>();

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
                _loggerMock.Object,
                _publisherMock.Object
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
                    It.IsAny<UserByIdWithRoleSpecification>(),
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
                r => r.GetWithSpecAsync(It.IsAny<UserByIdWithRoleSpecification>(), It.IsAny<bool>()),
                Times.Once);
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