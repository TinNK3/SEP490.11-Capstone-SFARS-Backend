using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.Application.Dtos.Community;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.API.Controller;

/// <summary>Community post – mọi user đã đăng nhập đều dùng được.</summary>
[ApiController]
[Authorize]
public class CommunityController : ControllerBase
{
    private readonly ICommunityPostService _svc;

    public CommunityController(ICommunityPostService svc) => _svc = svc;

    /// <summary>Danh sách bài đăng cộng đồng (phân trang)</summary>
    [HttpGet(APIRoute.Community.GetPosts, Name = nameof(GetPostsAsync))]
    public async Task<IActionResult> GetPostsAsync(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => this.ToIActionResult(await _svc.GetPostsAsync(page, pageSize, User.GetUserId()));

    /// <summary>Chi tiết bài đăng</summary>
    [HttpGet(APIRoute.Community.GetPostById, Name = nameof(GetPostByIdAsync))]
    public async Task<IActionResult> GetPostByIdAsync(Guid id)
        => this.ToIActionResult(await _svc.GetPostByIdAsync(id, User.GetUserId()));

    /// <summary>Tạo bài đăng mới (hiển thị ngay, không cần duyệt)</summary>
    [HttpPost(APIRoute.Community.CreatePost, Name = nameof(CreatePostAsync))]
    public async Task<IActionResult> CreatePostAsync([FromBody] CreatePostRequest req)
        => this.ToIActionResult(await _svc.CreatePostAsync(User.GetUserId(), req.Content, req.MediaUrls));

    /// <summary>Xóa bài đăng (chỉ tác giả)</summary>
    [HttpDelete(APIRoute.Community.DeletePost, Name = nameof(DeletePostAsync))]
    public async Task<IActionResult> DeletePostAsync(Guid id)
        => this.ToIActionResult(await _svc.DeletePostAsync(id, User.GetUserId()));

    /// <summary>Toggle like / unlike</summary>
    [HttpPost(APIRoute.Community.ToggleLike, Name = nameof(ToggleLikeAsync))]
    public async Task<IActionResult> ToggleLikeAsync(Guid id)
        => this.ToIActionResult(await _svc.ToggleLikeAsync(id, User.GetUserId()));

    /// <summary>Danh sách comment của bài đăng</summary>
    [HttpGet(APIRoute.Community.GetComments, Name = nameof(GetCommentsAsync))]
    public async Task<IActionResult> GetCommentsAsync(Guid id)
        => this.ToIActionResult(await _svc.GetCommentsAsync(id));

    /// <summary>Thêm comment hoặc reply</summary>
    [HttpPost(APIRoute.Community.AddComment, Name = nameof(AddCommentAsync))]
    public async Task<IActionResult> AddCommentAsync(
        Guid id, [FromBody] CreateCommentRequest req)
        => this.ToIActionResult(await _svc.AddCommentAsync(id, User.GetUserId(), req.Content, req.ParentId));

    /// <summary>Xóa comment (soft delete, chỉ tác giả)</summary>
    [HttpDelete(APIRoute.Community.DeleteComment, Name = nameof(DeleteCommentAsync))]
    public async Task<IActionResult> DeleteCommentAsync(Guid commentId)
        => this.ToIActionResult(await _svc.DeleteCommentAsync(commentId, User.GetUserId()));
}
