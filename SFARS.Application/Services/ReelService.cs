using Microsoft.EntityFrameworkCore;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Reels;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.Reels;

namespace SFARS.Application.Services;

public class ReelService : IReelService
{
    private readonly IUnitOfWork _uow;
    private readonly IFileStorageService _fileStorageService;

    public ReelService(IUnitOfWork uow, IFileStorageService fileStorageService)
    {
        _uow = uow;
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

            await _uow.Repository<Reel, Guid>().AddAsync(newReel);
            await _uow.SaveChangesAsync();

            var responseDto = await MapToReelResponseDtoAsync(newReel, currentUserId);
            return new ServiceResult(ResultCodeConst.SYS_Success0001, "Reel created successfully", responseDto);
        }
        catch (Exception ex)
        {
            return new ServiceResult(ResultCodeConst.SYS_Fail0001, $"Failed to upload reel: {ex.Message}");
        }
    }

    public async Task<IServiceResult> GetReelsFeedAsync(Guid? currentUserId, int pageNumber, int pageSize)
    {
        var repo = _uow.Repository<Reel, Guid>();
        
        var totalCount = await repo.CountAsync(ReelSpecification.ForCount());
        var reels = await repo.GetAllWithSpecAsync(ReelSpecification.Feed(currentUserId, (pageNumber - 1) * pageSize, pageSize), tracked: false);

        var likeRepo = _uow.Repository<ReelLike, object>();
        var reelList = new List<ReelResponseDto>();

        foreach (var r in reels)
        {
            var isLiked = currentUserId.HasValue && await likeRepo.AnyAsync(l => l.ReelId == r.Id && l.UserId == currentUserId.Value);
            reelList.Add(new ReelResponseDto
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
                IsLikedByCurrentUser = isLiked
            });
        }

        var response = new ReelListResponse(reelList, totalCount, pageNumber, pageSize);
        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Success", response);
    }

    public async Task<IServiceResult> GetReelsByUserIdAsync(Guid? currentUserId, Guid targetUserId, int pageNumber, int pageSize)
    {
        var repo = _uow.Repository<Reel, Guid>();

        var totalCount = await repo.CountAsync(ReelSpecification.ForCount(targetUserId));
        var reels = await repo.GetAllWithSpecAsync(ReelSpecification.ByUserId(targetUserId, (pageNumber - 1) * pageSize, pageSize), tracked: false);

        var likeRepo = _uow.Repository<ReelLike, object>();
        var reelList = new List<ReelResponseDto>();

        foreach (var r in reels)
        {
            var isLiked = currentUserId.HasValue && await likeRepo.AnyAsync(l => l.ReelId == r.Id && l.UserId == currentUserId.Value);
            reelList.Add(new ReelResponseDto
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
                IsLikedByCurrentUser = isLiked
            });
        }

        var response = new ReelListResponse(reelList, totalCount, pageNumber, pageSize);
        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Success", response);
    }

    public async Task<IServiceResult> GetReelByIdAsync(Guid? currentUserId, Guid reelId)
    {
        var spec = new BaseSpecification<Reel>(r => r.Id == reelId && !r.IsHidden);
        spec.ApplyInclude(q => q.Include(r => r.User));

        var reel = await _uow.Repository<Reel, Guid>().GetWithSpecAsync(spec, tracked: false);

        if (reel == null) return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Reel not found");

        var isLiked = currentUserId.HasValue && await _uow.Repository<ReelLike, object>().AnyAsync(l => l.ReelId == reel.Id && l.UserId == currentUserId.Value);

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
            IsLikedByCurrentUser = isLiked
        };

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Success", responseDto);
    }

    public async Task<IServiceResult> HideReelAsync(Guid currentUserId, Guid reelId)
    {
        var repo = _uow.Repository<Reel, Guid>();
        var reel = await repo.GetByIdAsync(reelId);
        if (reel == null) return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Reel not found");

        if (reel.UserId != currentUserId)
            return new ServiceResult(ResultCodeConst.SYS_Warning0007, "Not authorized to hide this reel");

        reel.IsHidden = true;
        repo.Update(reel);
        await _uow.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Reel hidden successfully");
    }

    public async Task<IServiceResult> DeleteReelAsync(Guid currentUserId, Guid reelId)
    {
        var repo = _uow.Repository<Reel, Guid>();
        var reel = await repo.GetByIdAsync(reelId);
        if (reel == null) return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Reel not found");

        if (reel.UserId != currentUserId)
            return new ServiceResult(ResultCodeConst.SYS_Warning0007, "Not authorized to delete this reel");

        // Delete from Cloudinary
        await _fileStorageService.DeleteAsync(reel.CloudinaryPublicId);

        await repo.DeleteAsync(reelId);
        await _uow.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Reel deleted successfully");
    }

    public async Task<IServiceResult> ToggleLikeAsync(Guid currentUserId, Guid reelId)
    {
        var reelRepo = _uow.Repository<Reel, Guid>();
        var reel = await reelRepo.GetByIdAsync(reelId);
        if (reel == null) return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Reel not found");

        var likeRepo = _uow.Repository<ReelLike, object>();
        var spec = new BaseSpecification<ReelLike>(l => l.ReelId == reelId && l.UserId == currentUserId);
        var existingLike = await likeRepo.GetWithSpecAsync(spec, tracked: true);

        bool isLiked;
        if (existingLike != null)
        {
            // GenericRepository.DeleteAsync(TKey) won't work for composite key easily
            // We use the context directly or a specialized delete if available
            // but since we have tracked entity, we can maybe use a Remove method if added to Repo
            // Since GenericRepository doesn't have a plain Remove(entity), I'll use DeleteWithSpec
            await likeRepo.DeleteWithSpecAsync(spec);
            reel.LikeCount = Math.Max(0, reel.LikeCount - 1);
            isLiked = false;
        }
        else
        {
            await likeRepo.AddAsync(new ReelLike { ReelId = reelId, UserId = currentUserId });
            reel.LikeCount++;
            isLiked = true;
        }

        reelRepo.Update(reel);
        await _uow.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Toggle like success", new { isLiked });
    }

    public async Task<IServiceResult> AddCommentAsync(Guid currentUserId, Guid reelId, string content, Guid? parentCommentId)
    {
        var reelRepo = _uow.Repository<Reel, Guid>();
        var reel = await reelRepo.GetByIdAsync(reelId);
        if (reel == null) return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Reel không tồn tại.");

        var commentRepo = _uow.Repository<ReelComment, Guid>();
        Guid? actualParentId = parentCommentId;

        if (parentCommentId.HasValue)
        {
            var parentSpec = new BaseSpecification<ReelComment>(c => c.Id == parentCommentId.Value);
            parentSpec.ApplyInclude(q => q.Include(c => c.ParentComment!).ThenInclude(p => p.ParentComment!).ThenInclude(p => p.ParentComment!));
            
            var parent = await commentRepo.GetWithSpecAsync(parentSpec, tracked: false);
            if (parent == null || parent.ReelId != reelId) 
                return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Comment cha không hợp lệ.");

            // Tính toán depth (0: Root, 1, 2, 3...)
            int depth = 0;
            var temp = parent;
            while (temp.ParentCommentId != null)
            {
                depth++;
                if (temp.ParentComment != null) temp = temp.ParentComment;
                else break;
            }

            // Nếu depth >= 3 (cấp 4), ép về cùng cấp với cha
            if (depth >= 3)
            {
                actualParentId = parent.ParentCommentId;
            }
        }

        var newComment = new ReelComment
        {
            ReelId = reelId,
            UserId = currentUserId,
            ParentCommentId = actualParentId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        await commentRepo.AddAsync(newComment);
        
        reel.CommentCount++;
        reelRepo.Update(reel);
        await _uow.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Thêm bình luận thành công");
    }

    public async Task<IServiceResult> GetCommentsAsync(Guid reelId, int pageNumber, int pageSize)
    {
        var repo = _uow.Repository<ReelComment, Guid>();
        var totalCount = await repo.CountAsync(ReelCommentSpecification.ForCount(reelId));
        
        if (totalCount == 0)
        {
            return new ServiceResult(ResultCodeConst.SYS_Success0001, "Thành công", 
                new ReelCommentListResponse(new List<ReelCommentResponseDto>(), 0, pageNumber, pageSize));
        }

        var skip = (pageNumber - 1) * pageSize;
        var spec = ReelCommentSpecification.ParentComments(reelId, skip, pageSize);
        var comments = await repo.GetAllWithSpecAsync(spec, tracked: false);

        var dtos = comments.Select(c => new ReelCommentResponseDto
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
        }).ToList();

        var response = new ReelCommentListResponse(dtos, totalCount, pageNumber, pageSize);
        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Thành công", response);
    }

    public async Task<IServiceResult> GetSubCommentsAsync(Guid parentCommentId, int pageNumber, int pageSize)
    {
        var repo = _uow.Repository<ReelComment, Guid>();
        var totalCount = await repo.CountAsync(ReelCommentSpecification.ForSubCount(parentCommentId));

        if (totalCount == 0)
        {
            return new ServiceResult(ResultCodeConst.SYS_Success0001, "Thành công", 
                new ReelCommentListResponse(new List<ReelCommentResponseDto>(), 0, pageNumber, pageSize));
        }

        var skip = (pageNumber - 1) * pageSize;
        var spec = ReelCommentSpecification.SubComments(parentCommentId, skip, pageSize);
        var comments = await repo.GetAllWithSpecAsync(spec, tracked: false);

        var dtos = comments.Select(c => new ReelCommentResponseDto
        {
            Id = c.Id,
            ReelId = c.ReelId,
            UserId = c.UserId,
            UserFullName = c.User.FullName,
            UserAvatar = c.User.Avatar,
            Content = c.Content,
            CreatedAt = c.CreatedAt,
            ParentCommentId = c.ParentCommentId,
            SubCommentCount = c.SubComments.Count() // Hỗ trợ nếu tiếp tục có cấp sâu hơn (đã ép về cùng cấp)
        }).ToList();

        var response = new ReelCommentListResponse(dtos, totalCount, pageNumber, pageSize);
        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Thành công", response);
    }

    public async Task<IServiceResult> DeleteCommentAsync(Guid currentUserId, Guid commentId)
    {
        var repo = _uow.Repository<ReelComment, Guid>();
        var comment = await repo.GetByIdAsync(commentId);
        if (comment == null) return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Comment not found");

        if (comment.UserId != currentUserId)
            return new ServiceResult(ResultCodeConst.SYS_Warning0007, "Not authorized to delete this comment");

        var reelRepo = _uow.Repository<Reel, Guid>();
        var reel = await reelRepo.GetByIdAsync(comment.ReelId);
        if (reel != null)
        {
            reel.CommentCount = Math.Max(0, reel.CommentCount - 1);
            reelRepo.Update(reel);
        }

        await repo.DeleteAsync(commentId);
        await _uow.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Comment deleted successfully");
    }

    private async Task<ReelResponseDto> MapToReelResponseDtoAsync(Reel reel, Guid currentUserId)
    {
        string userFullName = "Unknown";
        string? userAvatar = null;

        var user = await _uow.Repository<User, Guid>().GetByIdAsync(reel.UserId);
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
