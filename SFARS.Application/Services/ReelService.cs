using Microsoft.EntityFrameworkCore;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Reels;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Infrastructure.Data.Context;

namespace SFARS.Application.Services;

public class ReelService : IReelService
{
    private readonly SFARSDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public ReelService(SFARSDbContext context, IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    public async Task<IServiceResult> CreateReelAsync(Guid currentUserId, string? caption, Stream videoStream, string fileName, string contentType, long contentLength)
    {
        if (contentLength > 10 * 1024 * 1024)
        {
            return new ServiceResult(ResultCodeConst.SYS_Warning0008, "Video size cannot exceed 10MB");
        }

        if (!contentType.StartsWith("video/"))
        {
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Only video files are allowed");
        }

        try
        {
            var uploadResult = await _fileStorageService.UploadAsync(
                videoStream, fileName, "reels", contentType);

            var newReel = new Reel
            {
                UserId = currentUserId,
                VideoUrl = uploadResult.Url,
                CloudinaryPublicId = uploadResult.PublicId,
                Caption = caption,
                IsHidden = false,
                LikeCount = 0,
                CommentCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Reels.AddAsync(newReel);
            await _context.SaveChangesAsync();

            var responseDto = await MapToReelResponseDtoAsync(newReel, currentUserId);
            return new ServiceResult(ResultCodeConst.SYS_Success0001, "Reel created successfully", responseDto);
        }
        catch (Exception ex)
        {
            return new ServiceResult(ResultCodeConst.SYS_Fail0001, $"Failed to upload reel: {ex.Message}");
        }
    }

    public async Task<IServiceResult> GetReelsFeedAsync(Guid currentUserId, int pageNumber, int pageSize)
    {
        var query = _context.Reels
            .Include(r => r.User)
            .Where(r => !r.IsHidden)
            .OrderByDescending(r => r.CreatedAt);

        var reels = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReelResponseDto
            {
                Id = r.Id,
                UserId = r.UserId,
                UserFullName = r.User.FullName,
                UserAvatar = r.User.Avatar,
                VideoUrl = r.VideoUrl,
                Caption = r.Caption,
                CreatedAt = r.CreatedAt,
                LikeCount = r.LikeCount,
                CommentCount = r.CommentCount,
                IsLikedByCurrentUser = _context.ReelLikes.Any(l => l.ReelId == r.Id && l.UserId == currentUserId)
            })
            .ToListAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Success", reels);
    }

    public async Task<IServiceResult> GetReelByIdAsync(Guid currentUserId, Guid reelId)
    {
        var reel = await _context.Reels
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == reelId && !r.IsHidden);

        if (reel == null) return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Reel not found");

        var responseDto = new ReelResponseDto
        {
            Id = reel.Id,
            UserId = reel.UserId,
            UserFullName = reel.User.FullName,
            UserAvatar = reel.User.Avatar,
            VideoUrl = reel.VideoUrl,
            Caption = reel.Caption,
            CreatedAt = reel.CreatedAt,
            LikeCount = reel.LikeCount,
            CommentCount = reel.CommentCount,
            IsLikedByCurrentUser = await _context.ReelLikes.AnyAsync(l => l.ReelId == reel.Id && l.UserId == currentUserId)
        };

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Success", responseDto);
    }

    public async Task<IServiceResult> HideReelAsync(Guid currentUserId, Guid reelId)
    {
        var reel = await _context.Reels.FindAsync(reelId);
        if (reel == null) return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Reel not found");

        if (reel.UserId != currentUserId)
            return new ServiceResult(ResultCodeConst.SYS_Warning0007, "Not authorized to hide this reel");

        reel.IsHidden = true;
        _context.Reels.Update(reel);
        await _context.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Reel hidden successfully");
    }

    public async Task<IServiceResult> DeleteReelAsync(Guid currentUserId, Guid reelId)
    {
        var reel = await _context.Reels.FindAsync(reelId);
        if (reel == null) return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Reel not found");

        if (reel.UserId != currentUserId)
            return new ServiceResult(ResultCodeConst.SYS_Warning0007, "Not authorized to delete this reel");

        // Delete from Cloudinary
        await _fileStorageService.DeleteAsync(reel.CloudinaryPublicId);

        _context.Reels.Remove(reel);
        await _context.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Reel deleted successfully");
    }

    public async Task<IServiceResult> ToggleLikeAsync(Guid currentUserId, Guid reelId)
    {
        var reel = await _context.Reels.FindAsync(reelId);
        if (reel == null) return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Reel not found");

        var existingLike = await _context.ReelLikes
            .FirstOrDefaultAsync(l => l.ReelId == reelId && l.UserId == currentUserId);

        bool isLiked;
        if (existingLike != null)
        {
            _context.ReelLikes.Remove(existingLike);
            reel.LikeCount = Math.Max(0, reel.LikeCount - 1);
            isLiked = false;
        }
        else
        {
            await _context.ReelLikes.AddAsync(new ReelLike { ReelId = reelId, UserId = currentUserId });
            reel.LikeCount++;
            isLiked = true;
        }

        _context.Reels.Update(reel);
        await _context.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Toggle like success", new { isLiked });
    }

    public async Task<IServiceResult> AddCommentAsync(Guid currentUserId, Guid reelId, string content, Guid? parentCommentId)
    {
        var reel = await _context.Reels.FindAsync(reelId);
        if (reel == null) return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Reel not found");

        if (parentCommentId.HasValue)
        {
            var parentExists = await _context.ReelComments.AnyAsync(c => c.Id == parentCommentId.Value);
            if (!parentExists) return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Parent comment not found");
        }

        var newComment = new ReelComment
        {
            ReelId = reelId,
            UserId = currentUserId,
            ParentCommentId = parentCommentId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        await _context.ReelComments.AddAsync(newComment);
        
        // Update comment count on Reel
        reel.CommentCount++;
        _context.Reels.Update(reel);

        await _context.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Comment added successfully");
    }

    public async Task<IServiceResult> GetCommentsAsync(Guid reelId, int pageNumber, int pageSize)
    {
        var query = _context.ReelComments
            .Include(c => c.User)
            .Where(c => c.ReelId == reelId && c.ParentCommentId == null)
            .OrderByDescending(c => c.CreatedAt);

        var comments = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ReelCommentResponseDto
            {
                Id = c.Id,
                ReelId = c.ReelId,
                UserId = c.UserId,
                UserFullName = c.User.FullName,
                UserAvatar = c.User.Avatar,
                Content = c.Content,
                CreatedAt = c.CreatedAt,
                ParentCommentId = c.ParentCommentId,
                SubCommentCount = c.SubComments.Count()
            })
            .ToListAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Success", comments);
    }

    public async Task<IServiceResult> GetSubCommentsAsync(Guid parentCommentId, int pageNumber, int pageSize)
    {
        var query = _context.ReelComments
            .Include(c => c.User)
            .Where(c => c.ParentCommentId == parentCommentId)
            .OrderBy(c => c.CreatedAt);

        var comments = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ReelCommentResponseDto
            {
                Id = c.Id,
                ReelId = c.ReelId,
                UserId = c.UserId,
                UserFullName = c.User.FullName,
                UserAvatar = c.User.Avatar,
                Content = c.Content,
                CreatedAt = c.CreatedAt,
                ParentCommentId = c.ParentCommentId,
                SubCommentCount = 0
            })
            .ToListAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Success", comments);
    }

    public async Task<IServiceResult> DeleteCommentAsync(Guid currentUserId, Guid commentId)
    {
        var comment = await _context.ReelComments.FindAsync(commentId);
        if (comment == null) return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Comment not found");

        if (comment.UserId != currentUserId)
            return new ServiceResult(ResultCodeConst.SYS_Warning0007, "Not authorized to delete this comment");

        var reel = await _context.Reels.FindAsync(comment.ReelId);
        if (reel != null)
        {
            reel.CommentCount = Math.Max(0, reel.CommentCount - 1);
            _context.Reels.Update(reel);
        }

        _context.ReelComments.Remove(comment);
        await _context.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Comment deleted successfully");
    }

    private async Task<ReelResponseDto> MapToReelResponseDtoAsync(Reel reel, Guid currentUserId)
    {
        // Require explicit User load before calling or check if it's there
        string userFullName = "Unknown";
        string? userAvatar = null;

        var user = await _context.Users.FindAsync(reel.UserId);
        if(user != null)
        {
            userFullName = user.FullName;
            userAvatar = user.Avatar;
        }

        return new ReelResponseDto
        {
            Id = reel.Id,
            UserId = reel.UserId,
            UserFullName = userFullName,
            UserAvatar = userAvatar,
            VideoUrl = reel.VideoUrl,
            Caption = reel.Caption,
            CreatedAt = reel.CreatedAt,
            LikeCount = reel.LikeCount,
            CommentCount = reel.CommentCount,
            IsLikedByCurrentUser = false // just created
        };
    }
}
