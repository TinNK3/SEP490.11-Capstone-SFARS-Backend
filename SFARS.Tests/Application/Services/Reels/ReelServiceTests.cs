using FluentAssertions;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Reels;
using SFARS.Application.Services;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Specifications.Interfaces;
using SFARS.Domain.Interfaces.Repositories.Base;
using System.Linq.Expressions;

namespace SFARS.Tests.Application.Services.Reels;

public class ReelServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IFileStorageService> _fileStorageMock;
    private readonly Mock<IGenericRepository<Reel, Guid>> _reelRepoMock;
    private readonly Mock<IGenericRepository<ReelLike, object>> _likeRepoMock;
    private readonly Mock<IGenericRepository<User, Guid>> _userRepoMock;
    private readonly Mock<IGenericRepository<ReelComment, Guid>> _commentRepoMock;

    private readonly ReelService _sut;

    public ReelServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _fileStorageMock = new Mock<IFileStorageService>();
        _reelRepoMock = new Mock<IGenericRepository<Reel, Guid>>();
        _likeRepoMock = new Mock<IGenericRepository<ReelLike, object>>();
        _userRepoMock = new Mock<IGenericRepository<User, Guid>>();
        _commentRepoMock = new Mock<IGenericRepository<ReelComment, Guid>>();

        _uowMock.Setup(u => u.Repository<Reel, Guid>()).Returns(_reelRepoMock.Object);
        _uowMock.Setup(u => u.Repository<ReelLike, object>()).Returns(_likeRepoMock.Object);
        _uowMock.Setup(u => u.Repository<User, Guid>()).Returns(_userRepoMock.Object);
        _uowMock.Setup(u => u.Repository<ReelComment, Guid>()).Returns(_commentRepoMock.Object);

        _sut = new ReelService(_uowMock.Object, _fileStorageMock.Object);
    }

    [Fact]
    public async Task GetReelsFeedAsync_Anonymous_ReturnsFeedWithoutLikes()
    {
        // Arrange
        var reels = new List<Reel>
        {
            new Reel { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), User = new User { FirstName = "User", LastName = "1" }, CreatedAt = DateTime.UtcNow }
        };

        _reelRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<Reel>>())).ReturnsAsync(1);
        _reelRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Reel>>(), false)).ReturnsAsync(reels);
        _likeRepoMock.Setup(l => l.AnyAsync(It.IsAny<Expression<Func<ReelLike, bool>>>())).ReturnsAsync(false);

        // Act
        var result = await _sut.GetReelsFeedAsync(null, 1, 5);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        var data = result.Data as ReelListResponse;
        data.Should().NotBeNull();
        data!.Items.Should().HaveCount(1);
        data.Items[0].IsLikedByCurrentUser.Should().BeFalse();
        data.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetReelsFeedAsync_Authenticated_ReturnsFeedWithLikes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reels = new List<Reel>
        {
            new Reel { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), User = new User { FirstName = "User", LastName = "1" }, CreatedAt = DateTime.UtcNow }
        };

        _reelRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<Reel>>())).ReturnsAsync(1);
        _reelRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Reel>>(), false)).ReturnsAsync(reels);
        _likeRepoMock.Setup(l => l.AnyAsync(It.IsAny<Expression<Func<ReelLike, bool>>>())).ReturnsAsync(true);

        // Act
        var result = await _sut.GetReelsFeedAsync(userId, 1, 5);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        var data = result.Data as ReelListResponse;
        data!.Items[0].IsLikedByCurrentUser.Should().BeTrue();
    }

    [Fact]
    public async Task GetReelsByUserIdAsync_ReturnsUserReelsWithMetadata()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var reels = new List<Reel>
        {
            new Reel { Id = Guid.NewGuid(), UserId = targetUserId, User = new User { FirstName = "Target", LastName = "User" }, CreatedAt = DateTime.UtcNow }
        };

        _reelRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<Reel>>())).ReturnsAsync(1);
        _reelRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Reel>>(), false)).ReturnsAsync(reels);

        // Act
        var result = await _sut.GetReelsByUserIdAsync(Guid.NewGuid(), targetUserId, 1, 5);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        var data = result.Data as ReelListResponse;
        data.Should().NotBeNull();
        data!.TotalCount.Should().Be(1);
        data.PageNumber.Should().Be(1);
        data.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task CreateReelAsync_LargeFile_ReturnsWarning()
    {
        // Act
        var result = await _sut.CreateReelAsync(Guid.NewGuid(), "Test", new MemoryStream(), "test.mp4", "video/mp4", 21 * 1024 * 1024);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0008);
        result.Message.Should().Contain("20MB");
    }

    [Fact]
    public async Task AddCommentAsync_ReplyVaoLevel4_SeBiEpVeCap4()
    {
        // Arrange: Tạo cây 4 cấp (0, 1, 2, 3)
        var reelId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        var l1Id = Guid.NewGuid();
        var l2Id = Guid.NewGuid();
        var l3Id = Guid.NewGuid();

        var l3Cmt = new ReelComment 
        { 
            Id = l3Id, ReelId = reelId, ParentCommentId = l2Id,
            ParentComment = new ReelComment 
            { 
                Id = l2Id, ParentCommentId = l1Id,
                ParentComment = new ReelComment 
                { 
                    Id = l1Id, ParentCommentId = rootId,
                    ParentComment = new ReelComment { Id = rootId, ParentCommentId = null }
                }
            }
        };

        _reelRepoMock.Setup(r => r.GetByIdAsync(reelId)).ReturnsAsync(new Reel { Id = reelId });
        _commentRepoMock.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<ReelComment>>(), false)).ReturnsAsync(l3Cmt);
        
        // Act
        var result = await _sut.AddCommentAsync(authorId, reelId, "Reply to level 4", l3Id);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        _commentRepoMock.Verify(r => r.AddAsync(It.Is<ReelComment>(c => c.ParentCommentId == l2Id)), Times.Once);
    }

    [Fact]
    public async Task GetCommentsAsync_PhanTrang_ThanhCong()
    {
        // Arrange
        var reelId = Guid.NewGuid();
        var comments = new List<ReelComment>
        {
            new ReelComment { Id = Guid.NewGuid(), ReelId = reelId, User = new User { FirstName = "A", LastName = "B" }, SubComments = new List<ReelComment>() }
        };

        _commentRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<ReelComment>>())).ReturnsAsync(1);
        _commentRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<ReelComment>>(), false)).ReturnsAsync(comments);

        // Act
        var result = await _sut.GetCommentsAsync(reelId, 1, 10);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        var data = result.Data as ReelCommentListResponse;
        data.Should().NotBeNull();
        data!.Items.Should().HaveCount(1);
    }
}
