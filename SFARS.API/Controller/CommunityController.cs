using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Community;
using SFARS.Application.Dtos.Community;
using SFARS.Domain.Common.Constants;
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
    [AllowAnonymous]
    public async Task<IActionResult> GetPostsAsync([FromQuery] CommunityPostSpecParams specParams)
        => this.ToIActionResult(await _svc.GetPostsAsync(specParams, User.GetUserIdOrNull()));

    /// <summary>Danh sách tất cả nội dung (Post & Reel) của một người cụ thể</summary>
    [HttpGet(APIRoute.Community.GetUserContent, Name = nameof(GetUserContentAsync))]
    [AllowAnonymous]
    public async Task<IActionResult> GetUserContentAsync(Guid userId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        => this.ToIActionResult(await _svc.GetUserContentAsync(userId, User.GetUserIdOrNull(), pageNumber, pageSize));

    /// <summary>Chi tiết bài đăng</summary>
    [HttpGet(APIRoute.Community.GetPostById, Name = nameof(GetPostByIdAsync))]
    [AllowAnonymous]
    public async Task<IActionResult> GetPostByIdAsync(Guid id)
        => this.ToIActionResult(await _svc.GetPostByIdAsync(id, User.GetUserIdOrNull()));

    /// <summary>Tạo bài đăng mới (hiển thị ngay, không cần duyệt)</summary>
    [HttpPost(APIRoute.Community.CreatePost, Name = nameof(CreatePostAsync))]
    public async Task<IActionResult> CreatePostAsync([FromForm] CreateCommunityPostRequest req)
    {
        var dtos = req.MediaFiles?.Select(f => new MediaUploadInfo(f.OpenReadStream(), f.FileName, f.ContentType)).ToList();
        return this.ToIActionResult(await _svc.CreatePostAsync(User.GetUserId(), req.Content, dtos));
    }

    /// <summary>Xóa bài đăng (tác giả hoặc Admin)</summary>
    [HttpDelete(APIRoute.Community.DeletePost, Name = nameof(DeletePostAsync))]
    public async Task<IActionResult> DeletePostAsync(Guid id)
        => this.ToIActionResult(await _svc.DeletePostAsync(id, User.GetUserId(), User.IsInRole(UserTypeConstants.Admin)));

    /// <summary>Sửa bài đăng (tác giả hoặc Admin)</summary>
    [HttpPut(APIRoute.Community.UpdatePost, Name = nameof(UpdatePostAsync))]
    public async Task<IActionResult> UpdatePostAsync(Guid id, [FromForm] UpdateCommunityPostRequest req)
    {
        var newFiles = req.NewMediaFiles?.Select(f => new MediaUploadInfo(f.OpenReadStream(), f.FileName, f.ContentType)).ToList();
        return this.ToIActionResult(await _svc.UpdatePostAsync(id, User.GetUserId(), req.Content, req.RetainedMediaUrls, newFiles, User.IsInRole(UserTypeConstants.Admin)));
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

    [HttpGet(APIRoute.Community.GetComments, Name = nameof(GetCommentsAsync))]
    [AllowAnonymous]
    public async Task<IActionResult> GetCommentsAsync(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        => this.ToIActionResult(await _svc.GetCommentsAsync(id, pageNumber, pageSize));

    /// <summary>Danh sách phản hồi của một comment</summary>
    [HttpGet(APIRoute.Community.GetSubComments, Name = nameof(GetSubCommentsAsync))]
    [AllowAnonymous]
    public async Task<IActionResult> GetSubCommentsAsync(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        => this.ToIActionResult(await _svc.GetSubCommentsAsync(id, pageNumber, pageSize));

    /// <summary>Thêm comment hoặc reply</summary>
    [HttpPost(APIRoute.Community.AddComment, Name = nameof(AddCommentAsync))]
    public async Task<IActionResult> AddCommentAsync(
        Guid id, [FromBody] CreateCommentRequest req)
        => this.ToIActionResult(await _svc.AddCommentAsync(id, User.GetUserId(), req.Content, req.ParentId));

    /// <summary>Xóa comment (soft delete, chỉ tác giả)</summary>
    [HttpDelete(APIRoute.Community.DeleteComment, Name = nameof(DeleteCommentAsync))]
    public async Task<IActionResult> DeleteCommentAsync(Guid commentId)
        => this.ToIActionResult(await _svc.DeleteCommentAsync(commentId, User.GetUserId()));

    /// <summary>Chia sẻ bài viết</summary>
    [HttpPost(APIRoute.Community.Share, Name = nameof(SharePostAsync))]
    public async Task<IActionResult> SharePostAsync(Guid id, [FromBody] ShareRequest req)
        => this.ToIActionResult(await _svc.SharePostAsync(id, User.GetUserId(), req.Content));

    /// <summary>Admin ẩn bài viết (có lý do)</summary>
    [HttpPatch(APIRoute.Community.AdminHidePost, Name = nameof(AdminHidePostAsync))]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminHidePostAsync(Guid id, [FromBody] SFARS.API.Payloads.Request.Admin.AdminModerationRequest req)
        => this.ToIActionResult(await _svc.AdminHidePostAsync(id, User.GetUserId(), req.Reason));

    /// <summary>Admin bỏ ẩn bài viết</summary>
    [HttpPatch(APIRoute.Community.AdminUnhidePost, Name = nameof(AdminUnhidePostAsync))]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUnhidePostAsync(Guid id)
        => this.ToIActionResult(await _svc.AdminUnhidePostAsync(id, User.GetUserId()));
}
