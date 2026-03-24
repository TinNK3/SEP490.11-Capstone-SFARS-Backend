using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Community;
using SFARS.Application.Services;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.Interfaces;
using SFARS.Domain.Specifications.Params;
using SFARS.Infrastructure.Hubs;
using System.Linq.Expressions;

namespace SFARS.Tests.Application.Services.Community;

public class CommunityPostServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IHubContext<CommunityHub>> _hubMock;
    private readonly Mock<ILogger<CommunityPostService>> _loggerMock;
    private readonly Mock<IGenericRepository<ContentPost, Guid>> _postRepoMock;
    private readonly Mock<IGenericRepository<PostComment, Guid>> _commentRepoMock;
    private readonly Mock<IFileStorageService> _fileStorageMock;

    private readonly CommunityPostService _sut; // System Under Test

    public CommunityPostServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _hubMock = new Mock<IHubContext<CommunityHub>>();
        _loggerMock = new Mock<ILogger<CommunityPostService>>();
        _fileStorageMock = new Mock<IFileStorageService>();

        _postRepoMock = new Mock<IGenericRepository<ContentPost, Guid>>();
        _commentRepoMock = new Mock<IGenericRepository<PostComment, Guid>>();

        // Setup IUnitOfWork trả về các repository giả lập (mocked repos)
        _uowMock.Setup(u => u.Repository<ContentPost, Guid>()).Returns(_postRepoMock.Object);
        _uowMock.Setup(u => u.Repository<PostComment, Guid>()).Returns(_commentRepoMock.Object);

        // Setup giả lập cho SignalR (IHubContext)
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        _hubMock.Setup(h => h.Clients).Returns(mockClients.Object);

        // Khởi tạo Service để test với các mock object đã cấu hình
        _sut = new CommunityPostService(
            _uowMock.Object,
            _hubMock.Object,
            _loggerMock.Object,
            _fileStorageMock.Object
        );
    }

    #region Tests cho CreatePostAsync
    [Fact]
    public async Task CreatePostAsync_TruyenThieuNoiDung_TraVeCanhBao()
    {
        // Act: Gửi nội dung rỗng và list media rỗng
        var result = await _sut.CreatePostAsync(Guid.NewGuid(), "", new List<MediaUploadInfo>());

        // Assert: Kỳ vọng trả về mã lỗi validate (Warning0001)
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        result.Message.Should().Contain("ít nhất 1 ảnh");
    }

    [Fact]
    public async Task CreatePostAsync_DuLieuHopLe_TaoThanhCongVaDaySignalR()
    {
        // Arrange: Chuẩn bị dữ liệu hợp lệ
        var authorId = Guid.NewGuid();
        var content = "This is a test post";

        var mockPost = new ContentPost
        {
            Id = Guid.NewGuid(),
            BodyContent = content,
            Type = PostType.Community,
            AuthorId = authorId,
            Author = new User { Id = authorId, FirstName = "Test", LastName = "User" },
            Medias = new List<PostMedia>(),
            Likes = new List<PostLike>(),
            CreatedAt = DateTime.UtcNow
        };

        _postRepoMock.Setup(r => r.AddAsync(It.IsAny<ContentPost>())).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _postRepoMock.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<ContentPost>>(), false))
            .ReturnsAsync(mockPost);

        // Act: Thực thi method tạo bài
        var result = await _sut.CreatePostAsync(authorId, content, null);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        result.Data.Should().BeOfType<CommunityPostDto>();
        _postRepoMock.Verify(r => r.AddAsync(It.IsAny<ContentPost>()), Times.Once); // Đảm bảo AddAsync gọi 1 lần
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once); // Đảm bảo SaveChangesAsync gọi 1 lần

        // Cực kì quan trọng: Đảm bảo SignalR đã được gọi để báo tin qua hàm FeedGroup
        _hubMock.Verify(h => h.Clients.Group(CommunityHub.FeedGroup), Times.Once);
    }
    #endregion

    #region Tests cho GetPostsAsync
    [Fact]
    public async Task GetPostsAsync_TraVeDanhSachCoPhanTrang()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var mockPosts = new List<ContentPost>
        {
            new ContentPost { Id = Guid.NewGuid(), Type = PostType.Community, Author = new User { Id = authorId, FirstName = "Test", LastName = "A" }, Medias = new List<PostMedia>(), Likes = new List<PostLike>() },
new ContentPost { Id = Guid.NewGuid(), Type = PostType.Community, Author = new User { Id = authorId, FirstName = "Test", LastName = "B" }, Medias = new List<PostMedia>(), Likes = new List<PostLike>() }
        };

        _postRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<ContentPost>>())).ReturnsAsync(2);
        _postRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<ContentPost>>(), false))
            .ReturnsAsync(mockPosts);

        // Act
        var result = await _sut.GetPostsAsync(new CommunityPostSpecParams { Page = 1, PageSize = 10 }, currentUserId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var data = result.Data as SFARS.Application.Dtos.PaginatedResultDto<CommunityPostDto>;
        data.Should().NotBeNull();
        data!.Pagination.TotalItems.Should().Be(2);
        data.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPostsAsync_WithSearch_ApplySearchFilterInCountSpec()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        _postRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<ContentPost>>())).ReturnsAsync(0);

        // Act
        var result = await _sut.GetPostsAsync(new CommunityPostSpecParams
        {
            Search = "snake"
        }, currentUserId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        _postRepoMock.Verify(r => r.CountAsync(It.Is<ISpecification<ContentPost>>(s => s.Filters.Count == 1)), Times.Once);
    }

    [Fact]
    public async Task GetPostsAsync_WithSort_ApplySortInListSpec()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var mockPosts = new List<ContentPost>
        {
            new ContentPost
            {
                Id = Guid.NewGuid(),
                Type = PostType.Community,
                Author = new User { Id = authorId, FirstName = "Sort", LastName = "Test" },
                Medias = new List<PostMedia>(),
                Likes = new List<PostLike>()
            }
        };

        ISpecification<ContentPost>? capturedListSpec = null;

        _postRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<ContentPost>>())).ReturnsAsync(1);
        _postRepoMock
            .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<ContentPost>>(), false))
            .Callback<ISpecification<ContentPost>, bool>((spec, _) => capturedListSpec = spec)
            .ReturnsAsync(mockPosts);

        // Act
        var result = await _sut.GetPostsAsync(new CommunityPostSpecParams
        {
            Sort = "LikeCount"
        }, currentUserId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        capturedListSpec.Should().NotBeNull();
        capturedListSpec!.OrderBy.Should().NotBeNull();
        capturedListSpec.OrderBy.Body.ToString().Should().Contain("LikeCount");
    }
    #endregion

    #region Tests cho DeletePostAsync
    [Fact]
    public async Task DeletePostAsync_BaiVietKhongTonTai_TraVeCanhBao()
    {
        // Arrange
        _postRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ContentPost?)null);

        // Act
        var result = await _sut.DeletePostAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
    }

    [Fact]
    public async Task DeletePostAsync_NguoiXoaKhongPhaiLaTacGia_TraVeLoiQuyen()
    {
        // Arrange
        var post = new ContentPost { Id = Guid.NewGuid(), Type = PostType.Community, AuthorId = Guid.NewGuid() };
        _postRepoMock.Setup(r => r.GetByIdAsync(post.Id)).ReturnsAsync(post);

        // Act (Xóa bằng một UserID khác với AuthorID)
        var result = await _sut.DeletePostAsync(post.Id, Guid.NewGuid());

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0007);
    }

    [Fact]
    public async Task DeletePostAsync_HopLe_TraVeSuccess()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var post = new ContentPost { Id = Guid.NewGuid(), Type = PostType.Community, AuthorId = authorId };

        _postRepoMock.Setup(r => r.GetByIdAsync(post.Id)).ReturnsAsync(post);
        _postRepoMock.Setup(r => r.DeleteAsync(post.Id)).ReturnsAsync(1);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.DeletePostAsync(post.Id, authorId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        _postRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }
    #endregion

    #region Tests cho ToggleLikeAsync
    [Fact]
    public async Task ToggleLikeAsync_ThemLikeThanhCongVaDaySignalR()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var post = new ContentPost { Id = postId, Type = PostType.Community, Likes = new List<PostLike>() };

        _postRepoMock.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<ContentPost>>(), true))
            .ReturnsAsync(post);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.ToggleLikeAsync(postId, userId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        post.Likes.Should().HaveCount(1); // Like count = 1
        post.LikeCount.Should().Be(1);

        var data = result.Data as LikeUpdatedPayload;
        data!.IsLiked.Should().BeTrue();

        // Đảm bảo event LikeUpdated đã được ban phát qua Broadcast PostGroup(postId)
        _hubMock.Verify(h => h.Clients.Group(CommunityHub.PostGroup(postId)), Times.Once);
    }
    #endregion
}
