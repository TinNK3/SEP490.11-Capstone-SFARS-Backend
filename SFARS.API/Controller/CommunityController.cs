using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Community;
using SFARS.Application.Dtos.Community;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Params;

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
    public async Task<IActionResult> GetPostsAsync([FromQuery] CommunityPostSpecParams specParams)
        => this.ToIActionResult(await _svc.GetPostsAsync(specParams, User.GetUserId()));

    /// <summary>Chi tiết bài đăng</summary>
    [HttpGet(APIRoute.Community.GetPostById, Name = nameof(GetPostByIdAsync))]
    public async Task<IActionResult> GetPostByIdAsync(Guid id)
        => this.ToIActionResult(await _svc.GetPostByIdAsync(id, User.GetUserId()));

    /// <summary>Tạo bài đăng mới (hiển thị ngay, không cần duyệt)</summary>
    [HttpPost(APIRoute.Community.CreatePost, Name = nameof(CreatePostAsync))]
    public async Task<IActionResult> CreatePostAsync([FromForm] CreateCommunityPostRequest req)
    {
        var dtos = req.MediaFiles?.Select(f => new MediaUploadInfo(f.OpenReadStream(), f.FileName, f.ContentType)).ToList();
        return this.ToIActionResult(await _svc.CreatePostAsync(User.GetUserId(), req.Content, dtos));
    }

    /// <summary>Xóa bài đăng (chỉ tác giả)</summary>
    [HttpDelete(APIRoute.Community.DeletePost, Name = nameof(DeletePostAsync))]
    public async Task<IActionResult> DeletePostAsync(Guid id)
        => this.ToIActionResult(await _svc.DeletePostAsync(id, User.GetUserId()));

    /// <summary>Sửa bài đăng (chỉ tác giả)</summary>
    [HttpPut(APIRoute.Community.UpdatePost, Name = nameof(UpdatePostAsync))]
    public async Task<IActionResult> UpdatePostAsync(Guid id, [FromForm] UpdateCommunityPostRequest req)
    {
        var newFiles = req.NewMediaFiles?.Select(f => new MediaUploadInfo(f.OpenReadStream(), f.FileName, f.ContentType)).ToList();
        return this.ToIActionResult(await _svc.UpdatePostAsync(id, User.GetUserId(), req.Content, req.RetainedMediaUrls, newFiles));
    }

    /// <summary>Ẩn bài đăng (chỉ tác giả)</summary>
    [HttpPatch(APIRoute.Community.HidePost, Name = nameof(HidePostAsync))]
    public async Task<IActionResult> HidePostAsync(Guid id)
        => this.ToIActionResult(await _svc.HidePostAsync(id, User.GetUserId()));

    /// <summary>Bỏ ẩn bài đăng (chỉ tác giả)</summary>
    [HttpPatch(APIRoute.Community.UnhidePost, Name = nameof(UnhidePostAsync))]
    public async Task<IActionResult> UnhidePostAsync(Guid id)
        => this.ToIActionResult(await _svc.UnhidePostAsync(id, User.GetUserId()));

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
