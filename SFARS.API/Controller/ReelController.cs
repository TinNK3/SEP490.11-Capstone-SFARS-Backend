using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Reels;
using SFARS.Application.Dtos.Reels;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.API.Controller;

/// <summary>Reel (Video ngắn) – chỉ những user đăng nhập mới dùng được.</summary>
[ApiController]
[Authorize]
public class ReelController : ControllerBase
{
    private readonly IReelService _svc;

    public ReelController(IReelService svc) => _svc = svc;

    /// <summary>Tạo mới 1 Reel (Upload Video max 10MB)</summary>
    [HttpPost(APIRoute.Reels.Create, Name = nameof(CreateReelAsync))]
    public async Task<IActionResult> CreateReelAsync([FromForm] CreateReelRequest request)
    {
        return this.ToIActionResult(await _svc.CreateReelAsync(
            User.GetUserId(),
            request.Caption,
            request.Video.OpenReadStream(),
            request.Video.FileName,
            request.Video.ContentType,
            request.Video.Length));
    }

    /// <summary>Lấy danh sách Feed Reel (Vuốt đến đâu load đến đó)</summary>
    [HttpGet(APIRoute.Reels.GetFeed, Name = nameof(GetReelsFeedAsync))]
    [AllowAnonymous]
    public async Task<IActionResult> GetReelsFeedAsync([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 5)
    {
        return this.ToIActionResult(await _svc.GetReelsFeedAsync(User.GetUserIdOrNull(), pageNumber, pageSize));
    }

    /// <summary>Lấy danh sách Reel của một User cụ thể</summary>
    [HttpGet(APIRoute.Reels.GetByUser, Name = nameof(GetReelsByUserIdAsync))]
    [AllowAnonymous]
    public async Task<IActionResult> GetReelsByUserIdAsync(Guid userId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 5)
    {
        return this.ToIActionResult(await _svc.GetReelsByUserIdAsync(User.GetUserIdOrNull(), userId, pageNumber, pageSize));
    }

    /// <summary>Lấy thông tin chi tiết 1 Reel (Nếu có URL chia sẻ)</summary>
    [HttpGet(APIRoute.Reels.GetById, Name = nameof(GetReelByIdAsync))]
    [AllowAnonymous]
    public async Task<IActionResult> GetReelByIdAsync(Guid id)
    {
        return this.ToIActionResult(await _svc.GetReelByIdAsync(User.GetUserIdOrNull(), id));
    }

    /// <summary>Ẩn 1 Reel (Chỉ tác giả)</summary>
    [HttpPatch(APIRoute.Reels.Hide, Name = nameof(HideReelAsync))]
    public async Task<IActionResult> HideReelAsync(Guid id)
    {
        return this.ToIActionResult(await _svc.HideReelAsync(User.GetUserId(), id));
    }

    /// <summary>Xoá 1 Reel (Chỉ tác giả) - Đồng thời giải phóng trên Cloudinary</summary>
    [HttpDelete(APIRoute.Reels.Delete, Name = nameof(DeleteReelAsync))]
    public async Task<IActionResult> DeleteReelAsync(Guid id)
    {
        return this.ToIActionResult(await _svc.DeleteReelAsync(User.GetUserId(), id));
    }

    /// <summary>Mở/tắt thả tim Reel</summary>
    [HttpPost(APIRoute.Reels.ToggleLike, Name = nameof(ToggleLikeReelAsync))]
    public async Task<IActionResult> ToggleLikeReelAsync(Guid id)
    {
        return this.ToIActionResult(await _svc.ToggleLikeAsync(User.GetUserId(), id));
    }

    /// <summary>Thêm bình luận mới vào Reel</summary>
    [HttpPost(APIRoute.Reels.AddComment, Name = nameof(AddReelCommentAsync))]
    public async Task<IActionResult> AddReelCommentAsync(Guid id, [FromBody] CreateReelCommentRequest request)
    {
        return this.ToIActionResult(await _svc.AddCommentAsync(User.GetUserId(), id, request.Content, request.ParentCommentId));
    }

    /// <summary>Lấy danh sách bình luận cha của 1 Reel (Vuốt đến đâu load đến đó)</summary>
    [HttpGet(APIRoute.Reels.GetComments, Name = nameof(GetReelCommentsAsync))]
    [AllowAnonymous]
    public async Task<IActionResult> GetReelCommentsAsync(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        return this.ToIActionResult(await _svc.GetCommentsAsync(id, pageNumber, pageSize));
    }

    /// <summary>Lấy danh sách các sub-comment (phản hồi) của một bình luận gốc</summary>
    [HttpGet(APIRoute.Reels.GetSubComments, Name = nameof(GetReelSubCommentsAsync))]
    [AllowAnonymous]
    public async Task<IActionResult> GetReelSubCommentsAsync(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 5)
    {
        return this.ToIActionResult(await _svc.GetSubCommentsAsync(id, pageNumber, pageSize));
    }

    /// <summary>Xoá bình luận (Chỉ tác giả)</summary>
    [HttpDelete(APIRoute.Reels.DeleteComment, Name = nameof(DeleteReelCommentAsync))]
    public async Task<IActionResult> DeleteReelCommentAsync(Guid id)
    {
        return this.ToIActionResult(await _svc.DeleteCommentAsync(User.GetUserId(), id));
    }
}
