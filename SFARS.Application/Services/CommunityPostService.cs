using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.Community;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Community;
using SFARS.Domain.Specifications.Params;
using SFARS.Domain.Specifications;
using SFARS.Infrastructure.Hubs;
using System.Linq;

namespace SFARS.Application.Services;

public class CommunityPostService : ICommunityPostService
{
    private readonly IUnitOfWork _uow;
    private readonly IHubContext<CommunityHub> _hub;
    private readonly ILogger<CommunityPostService> _logger;
    private readonly IFileStorageService _fileStorage;

    public CommunityPostService(
        IUnitOfWork uow,
        IHubContext<CommunityHub> hub,
        ILogger<CommunityPostService> logger,
        IFileStorageService fileStorage)
    {
        _uow = uow;
        _hub = hub;
        _logger = logger;
        _fileStorage = fileStorage;
    }

    public async Task<IServiceResult> GetPostsAsync(CommunityPostSpecParams specParams, Guid currentUserId)
    {
        specParams ??= new CommunityPostSpecParams();
        var page = specParams.GetPage();
        var limit = specParams.GetTake();

        var repo = _uow.Repository<ContentPost, Guid>();

        var total = await repo.CountAsync(CommunityPostSpecification.Count(specParams, currentUserId));

        if (total == 0)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0004,
                "Không có bài đăng.",
                new PaginatedResultDto<CommunityPostDto>(
                    Enumerable.Empty<CommunityPostDto>(),
                    page,
                    limit,
                    0,
                    0));
        }

        var spec = CommunityPostSpecification.List(specParams, currentUserId);

        var posts = await repo.GetAllWithSpecAsync(spec, tracked: false);

        var dtos = posts.Select(p => MapToDto(p, currentUserId)).ToList();
        var totalPages = (int)Math.Ceiling((double)total / limit);

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            "Lấy danh sách thành công",
            new PaginatedResultDto<CommunityPostDto>(dtos, page, limit, totalPages, total));
    }

    public async Task<IServiceResult> GetPostByIdAsync(Guid postId, Guid currentUserId)
    {
        var spec = new BaseSpecification<ContentPost>(p => p.Id == postId && p.Type == PostType.Community && (p.IsPublished || p.AuthorId == currentUserId));
        spec.ApplyInclude(q => q.Include(p => p.Author).Include(p => p.Medias).Include(p => p.Likes));

        var post = await _uow.Repository<ContentPost, Guid>().GetWithSpecAsync(spec, tracked: false);

        if (post is null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Bài đăng không tồn tại.");

        return new ServiceResult(ResultCodeConst.SYS_Success0002, "Lấy bài đăng thành công", MapToDto(post, currentUserId));
    }

    public async Task<IServiceResult> CreatePostAsync(Guid authorId, string? content, List<MediaUploadInfo>? mediaFiles)
    {
        if (string.IsNullOrWhiteSpace(content) && (mediaFiles is null || mediaFiles.Count == 0))
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Bài đăng phải có nội dung hoặc ít nhất 1 ảnh.");

        var post = new ContentPost
        {
            Title = content?.Length > 0 ? TruncateContent(content, 100) : "Community Post",
            Slug = $"post-{Guid.NewGuid():N}",
            BodyContent = content,
            Type = PostType.Community,
            AuthorId = authorId,
            IsPublished = true,
            CreatedBy = authorId
        };

        if (mediaFiles?.Count > 0)
        {
            for (int i = 0; i < mediaFiles.Count; i++)
            {
                var file = mediaFiles[i];

                var uploadResult = await _fileStorage.UploadAsync(
                    file.Stream,
                    file.FileName,
                    "community_posts",
                    file.ContentType);

                post.Medias.Add(new PostMedia
                {
                    Url = uploadResult.Url,
                    ContentType = file.ContentType,
                    Order = i,
                    CreatedBy = authorId
                });
            }
        }

        await _uow.Repository<ContentPost, Guid>().AddAsync(post);
        await _uow.SaveChangesAsync();

        var spec = new BaseSpecification<ContentPost>(p => p.Id == post.Id);
        spec.ApplyInclude(q => q.Include(p => p.Author).Include(p => p.Medias).Include(p => p.Likes));

        var created = await _uow.Repository<ContentPost, Guid>().GetWithSpecAsync(spec, tracked: false);
        var dto = MapToDto(created!, authorId);

        await _hub.Clients.Group(CommunityHub.FeedGroup).SendAsync("NewPost", new NewPostPayload(
            dto.Id, dto.Author, TruncateContent(dto.Content, 100), dto.Medias.Count, dto.CreatedAt));

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Tạo bài đăng thành công", dto);
    }

    public async Task<IServiceResult> UpdatePostAsync(Guid postId, Guid authorId, string? content, List<string>? retainedMediaUrls, List<MediaUploadInfo>? newMediaFiles)
    {
        var spec = new BaseSpecification<ContentPost>(p => p.Id == postId && p.Type == PostType.Community);
        // Không Include Medias để tránh lỗi EF Core Tracking khi thao tác xóa
        
        var post = await _uow.Repository<ContentPost, Guid>().GetWithSpecAsync(spec, tracked: true);

        if (post is null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Bài đăng không tồn tại.");

        if (post.AuthorId != authorId)
            return new ServiceResult(ResultCodeConst.SYS_Warning0007, "Bạn không có quyền sửa bài đăng này.");

        if (string.IsNullOrWhiteSpace(content) && (retainedMediaUrls is null || retainedMediaUrls.Count == 0) && (newMediaFiles is null || newMediaFiles.Count == 0))
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Bài đăng phải có nội dung hoặc ít nhất 1 ảnh.");

        post.BodyContent = content;
        post.Title = content?.Length > 0 ? TruncateContent(content, 100) : "Community Post";
        post.UpdatedAt = DateTime.UtcNow;
        post.UpdatedBy = authorId;

        var mediaRepo = _uow.Repository<PostMedia, Guid>();
        
        // Fetch riêng PostMedia mà không dính dáng tới ChangeTracker của list post.Medias
        var postMedias = await mediaRepo.GetAllWithSpecAsync(new BaseSpecification<PostMedia>(m => m.PostId == postId), tracked: false);
        var postMediasList = postMedias.ToList();

        var mediasToRemove = postMediasList.Where(m => retainedMediaUrls == null || !retainedMediaUrls.Contains(m.Url)).ToList();
        
        if (mediasToRemove.Any())
        {
            await mediaRepo.DeleteRangeAsync(mediasToRemove.Select(m => m.Id).ToArray());
        }

        var mediasToKeep = postMediasList.Where(m => !mediasToRemove.Contains(m)).ToList();
        int targetOrder = 0;

        // Cập nhật Order cho ảnh giữ lại (nếu cần)
        foreach (var media in mediasToKeep.OrderBy(m => m.Order))
        {
            if (media.Order != targetOrder)
            {
                media.Order = targetOrder;
                await mediaRepo.UpdateAsync(media); // Kích hoạt Modified state
            }
            targetOrder++;
        }

        // Upload file mới
        if (newMediaFiles?.Count > 0)
        {
            for (int i = 0; i < newMediaFiles.Count; i++)
            {
                var file = newMediaFiles[i];

                var uploadResult = await _fileStorage.UploadAsync(
                    file.Stream,
                    file.FileName,
                    "community_posts",
                    file.ContentType);

                var newMedia = new PostMedia
                {
                    PostId = postId,
                    Url = uploadResult.Url,
                    ContentType = file.ContentType,
                    Order = targetOrder++,
                    CreatedBy = authorId
                };
                
                await mediaRepo.AddAsync(newMedia); // Kích hoạt Added state
            }
        }

        await _uow.SaveChangesAsync();

        var returnSpec = new BaseSpecification<ContentPost>(p => p.Id == postId);
        returnSpec.ApplyInclude(q => q.Include(p => p.Author).Include(p => p.Medias).Include(p => p.Likes));

        var updated = await _uow.Repository<ContentPost, Guid>().GetWithSpecAsync(returnSpec, tracked: false);
        var dto = MapToDto(updated!, authorId);

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Sửa bài đăng thành công", dto);
    }

    public async Task<IServiceResult> HidePostAsync(Guid postId, Guid requesterId)
    {
        var post = await _uow.Repository<ContentPost, Guid>().GetByIdAsync(postId);
        if (post is null || post.Type != PostType.Community)
            return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Bài đăng không tồn tại.");

        if (post.AuthorId != requesterId)
            return new ServiceResult(ResultCodeConst.SYS_Warning0007, "Bạn không có quyền ẩn bài đăng này.");

        post.IsPublished = false;
        post.UpdatedAt = DateTime.UtcNow;
        post.UpdatedBy = requesterId;

        _uow.Repository<ContentPost, Guid>().Update(post);
        await _uow.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Ẩn bài đăng thành công", true);
    }

    public async Task<IServiceResult> UnhidePostAsync(Guid postId, Guid requesterId)
    {
        var post = await _uow.Repository<ContentPost, Guid>().GetByIdAsync(postId);
        if (post is null || post.Type != PostType.Community)
            return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Bài đăng không tồn tại.");

        if (post.AuthorId != requesterId)
            return new ServiceResult(ResultCodeConst.SYS_Warning0007, "Bạn không có quyền bỏ ẩn bài đăng này.");

        post.IsPublished = true;
        post.UpdatedAt = DateTime.UtcNow;
        post.UpdatedBy = requesterId;

        _uow.Repository<ContentPost, Guid>().Update(post);
        await _uow.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Bỏ ẩn bài đăng thành công", true);
    }

    public async Task<IServiceResult> DeletePostAsync(Guid postId, Guid requesterId)
    {
        var post = await _uow.Repository<ContentPost, Guid>().GetByIdAsync(postId);
        if (post is null || post.Type != PostType.Community)
            return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Bài đăng không tồn tại.");

        if (post.AuthorId != requesterId)
            return new ServiceResult(ResultCodeConst.SYS_Warning0007, "Bạn không có quyền xóa bài đăng này.");

        await _uow.Repository<ContentPost, Guid>().DeleteAsync(postId);
        await _uow.SaveChangesAsync();
        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Xóa thành công", true);
    }

    public async Task<IServiceResult> ToggleLikeAsync(Guid postId, Guid userId)
    {
        var spec = new BaseSpecification<ContentPost>(p => p.Id == postId && p.Type == PostType.Community);
        spec.ApplyInclude(q => q.Include(p => p.Likes));

        var post = await _uow.Repository<ContentPost, Guid>().GetWithSpecAsync(spec, tracked: true);

        if (post is null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Bài đăng không tồn tại.");

        var existing = post.Likes.FirstOrDefault(l => l.UserId == userId);
        bool isLiked;

        if (existing is not null)
        {
            post.Likes.Remove(existing);
            post.LikeCount = Math.Max(0, post.LikeCount - 1);
            isLiked = false;
        }
        else
        {
            post.Likes.Add(new PostLike { PostId = postId, UserId = userId });
            post.LikeCount++;
            isLiked = true;
        }

        post.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync();

        var payload = new LikeUpdatedPayload(postId, post.LikeCount, isLiked);
        await _hub.Clients.Group(CommunityHub.PostGroup(postId)).SendAsync("LikeUpdated", payload);

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Success", payload);
    }

    public async Task<IServiceResult> AddCommentAsync(Guid postId, Guid authorId, string content, Guid? parentId)
    {
        var post = await _uow.Repository<ContentPost, Guid>().GetByIdAsync(postId);
        if (post is null || post.Type != PostType.Community)
            return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Bài đăng không tồn tại.");

        if (parentId.HasValue)
        {
            var parent = await _uow.Repository<PostComment, Guid>().GetByIdAsync(parentId.Value);
            if (parent is null || parent.PostId != postId)
                return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Comment cha không hợp lệ.");
        }

        var comment = new PostComment
        {
            PostId = postId,
            AuthorId = authorId,
            Content = content,
            ParentId = parentId,
            CreatedBy = authorId
        };

        await _uow.Repository<PostComment, Guid>().AddAsync(comment);
        post.CommentCount++;
        post.UpdatedAt = DateTime.UtcNow;
        _uow.Repository<ContentPost, Guid>().Update(post);
        await _uow.SaveChangesAsync();

        var spec = new BaseSpecification<PostComment>(c => c.Id == comment.Id);
        spec.ApplyInclude(q => q.Include(c => c.Author));
        var saved = await _uow.Repository<PostComment, Guid>().GetWithSpecAsync(spec, tracked: false);

        var dto = MapCommentToDto(saved!);

        await _hub.Clients.Group(CommunityHub.PostGroup(postId)).SendAsync("NewComment", new NewCommentPayload(
            dto.Id, postId, dto.Author, dto.Content, dto.ParentId, dto.CreatedAt));

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Thành công", dto);
    }

    public async Task<IServiceResult> GetCommentsAsync(Guid postId)
    {
        var spec = new BaseSpecification<PostComment>(c => c.PostId == postId && c.ParentId == null && !c.IsDeleted);
        spec.AddOrderBy(c => c.CreatedAt);
        spec.ApplyInclude(q => q.Include(c => c.Author).Include(c => c.Replies).ThenInclude(r => r.Author));

        var comments = await _uow.Repository<PostComment, Guid>().GetAllWithSpecAsync(spec, tracked: false);
        var dtos = comments.Select(MapCommentToDto).ToList();

        return new ServiceResult(ResultCodeConst.SYS_Success0002, "Thành công", dtos);
    }

    public async Task<IServiceResult> DeleteCommentAsync(Guid commentId, Guid requesterId)
    {
        var comment = await _uow.Repository<PostComment, Guid>().GetByIdAsync(commentId);
        if (comment is null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Comment không tồn tại.");

        if (comment.AuthorId != requesterId)
            return new ServiceResult(ResultCodeConst.SYS_Warning0007, "Bạn không có quyền xóa comment này.");

        comment.IsDeleted = true;
        comment.UpdatedAt = DateTime.UtcNow;
        _uow.Repository<PostComment, Guid>().Update(comment);
        await _uow.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Xóa thành công", true);
    }

    private static CommunityPostDto MapToDto(ContentPost p, Guid viewerId) => new(
        p.Id,
        new PostAuthorDto(p.Author.Id, p.Author.FullName ?? "Unknown", p.Author.Avatar),
        p.BodyContent,
        p.Medias.OrderBy(m => m.Order).Select(m => new PostMediaDto(m.Id, m.Url, m.ContentType, m.Order)).ToList(),
        p.LikeCount,
        p.CommentCount,
        p.Likes.Any(l => l.UserId == viewerId),
        p.CreatedAt
    );

    private static PostCommentDto MapCommentToDto(PostComment c) => new(
        c.Id,
        c.PostId,
        new PostAuthorDto(c.Author.Id, c.Author.FullName ?? "Unknown", c.Author.Avatar),
        c.IsDeleted ? "[Đã xóa]" : c.Content,
        c.ParentId,
        c.CreatedAt,
        c.Replies.Where(r => !r.IsDeleted).OrderBy(r => r.CreatedAt).Select(MapCommentToDto).ToList()
    );

    private static string TruncateContent(string? s, int max) => s?.Length > max ? s[..max] + "…" : s ?? string.Empty;

    private static string GuessContentType(string url)
    {
        var ext = System.IO.Path.GetExtension(url).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            _ => "application/octet-stream"
        };
    }
}
