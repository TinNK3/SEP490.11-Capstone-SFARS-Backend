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
using SFARS.Application.Interfaces.Services;
using SFARS.Infrastructure.Hubs;
using System.Linq;

namespace SFARS.Application.Services;

public class CommunityPostService : ICommunityPostService
{
    private readonly IUnitOfWork _uow;
    private readonly IHubContext<CommunityHub> _hub;
    private readonly ILogger<CommunityPostService> _logger;
    private readonly IFileStorageService _fileStorage;
    private readonly INotificationService _notificationService;

    public CommunityPostService(
        IUnitOfWork uow,
        IHubContext<CommunityHub> hub,
        ILogger<CommunityPostService> logger,
        IFileStorageService fileStorage,
        INotificationService notificationService)
    {
        _uow = uow;
        _hub = hub;
        _logger = logger;
        _fileStorage = fileStorage;
        _notificationService = notificationService;
    }

    public async Task<IServiceResult> GetPostsAsync(CommunityPostSpecParams specParams, Guid? currentUserId)
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

        var dtos = posts.Select(p => MapToDto(p, currentUserId, limitMedia: true)).ToList();
        var totalPages = (int)Math.Ceiling((double)total / limit);

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            "Lấy danh sách thành công",
            new PaginatedResultDto<CommunityPostDto>(dtos, page, limit, totalPages, total));
    }

    public async Task<IServiceResult> GetPostByIdAsync(Guid postId, Guid? currentUserId)
    {
        var spec = new BaseSpecification<ContentPost>(p => p.Id == postId && p.Type == PostType.Community && (p.IsPublished || (currentUserId.HasValue && p.AuthorId == currentUserId.Value)));
        spec.ApplyInclude(q => q.Include(p => p.Author).Include(p => p.Medias).Include(p => p.Likes));

        var post = await _uow.Repository<ContentPost, Guid>().GetWithSpecAsync(spec, tracked: false);

        if (post is null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Bài đăng không tồn tại.");

        return new ServiceResult(ResultCodeConst.SYS_Success0002, "Lấy bài đăng thành công", MapToDto(post, currentUserId, limitMedia: false));
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
        var dto = MapToDto(created!, authorId, limitMedia: false);

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
        var dto = MapToDto(updated!, authorId, limitMedia: false);

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

        // Notify author if liked
        if (isLiked)
        {
            await _notificationService.NotifyLikeAsync(post.AuthorId, userId, postId, false);
        }

        // Broadcast update via SignalR
        await _hub.Clients.All.SendAsync("PostLiked", new { postId, likeCount = post.LikeCount, userId, isLiked });
        var payload = new LikeUpdatedPayload(postId, post.LikeCount, isLiked);
        await _hub.Clients.Group(CommunityHub.PostGroup(postId)).SendAsync("LikeUpdated", payload);

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Success", payload);
    }

    public async Task<IServiceResult> AddCommentAsync(Guid postId, Guid authorId, string content, Guid? parentId)
    {
        var post = await _uow.Repository<ContentPost, Guid>().GetByIdAsync(postId);
        if (post is null || post.Type != PostType.Community)
            return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Bài đăng không tồn tại.");

        Guid? actualParentId = parentId;

        if (parentId.HasValue)
        {
            // Truy vấn comment cha kèm theo hierarchy để tính toán Level
            var parentSpec = new BaseSpecification<PostComment>(c => c.Id == parentId.Value);
            parentSpec.ApplyInclude(q => q.Include(c => c.Parent!).ThenInclude(p => p.Parent!).ThenInclude(p => p.Parent!));
            
            var parent = await _uow.Repository<PostComment, Guid>().GetWithSpecAsync(parentSpec, tracked: false);
            if (parent is null || parent.PostId != postId)
                return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Comment cha không hợp lệ.");

            // Tính toán depth (0: Root, 1, 2, 3...)
            int depth = 0;
            var current = parent;
            while (current.ParentId != null)
            {
                depth++;
                if (current.Parent != null) current = current.Parent;
                else break;
            }

            // Nếu đang reply vào một comment ở cấp 4 (depth = 3), ta ép comment mới cũng ở cấp 4
            // bằng cách gán ParentId của nó giống hệt ParentId của comment cha này.
            if (depth >= 3)
            {
                actualParentId = parent.ParentId;
            }
        }

        var comment = new PostComment
        {
            PostId = postId,
            AuthorId = authorId,
            Content = content,
            ParentId = actualParentId, // Sử dụng actualParentId đã qua xử lý
            CreatedBy = authorId
        };

        await _uow.Repository<PostComment, Guid>().AddAsync(comment);
        post.CommentCount++;
        post.UpdatedAt = DateTime.UtcNow;
        _uow.Repository<ContentPost, Guid>().Update(post);
        await _uow.SaveChangesAsync();

        // Notify author of the post or the parent comment
        if (parentId.HasValue)
        {
            var parentComment = await _uow.Repository<PostComment, Guid>().GetByIdAsync(parentId.Value);
            if (parentComment != null)
            {
                await _notificationService.NotifyCommentReplyAsync(parentComment.AuthorId, authorId, postId, false);
            }
        }
        else
        {
            await _notificationService.NotifyCommentAsync(post.AuthorId, authorId, postId, false);
        }

        // Broadcast to SignalR
        await _hub.Clients.All.SendAsync("NewComment", new { postId, commentId = comment.Id, authorId });
        var spec = new BaseSpecification<PostComment>(c => c.Id == comment.Id);
        spec.ApplyInclude(q => q.Include(c => c.Author));
        var saved = await _uow.Repository<PostComment, Guid>().GetWithSpecAsync(spec, tracked: false);

        var dto = MapCommentToDto(saved!, 0);

        await _hub.Clients.Group(CommunityHub.PostGroup(postId)).SendAsync("NewComment", new NewCommentPayload(
            dto.Id, postId, dto.Author, dto.Content, dto.ParentId, dto.CreatedAt));

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Thành công", dto);
    }

    public async Task<IServiceResult> GetCommentsAsync(Guid postId, int pageNumber, int pageSize)
    {
        var repo = _uow.Repository<PostComment, Guid>();
        
        // Chỉ lấy comment cấp 1 (ParentId == null)
        var countSpec = new BaseSpecification<PostComment>(c => c.PostId == postId && c.ParentId == null && !c.IsDeleted);
        var totalItems = await repo.CountAsync(countSpec);

        if (totalItems == 0)
        {
            return new ServiceResult(ResultCodeConst.SYS_Success0002, "Thành công", 
                new PaginatedResultDto<PostCommentDto>(Enumerable.Empty<PostCommentDto>(), pageNumber, pageSize, 0, 0));
        }

        var spec = new BaseSpecification<PostComment>(c => c.PostId == postId && c.ParentId == null && !c.IsDeleted);
        spec.ApplyPaging(pageSize, (pageNumber - 1) * pageSize);
        spec.AddOrderBy(c => c.CreatedAt);
        spec.ApplyInclude(q => q.Include(c => c.Author).Include(c => c.Replies));

        var comments = await repo.GetAllWithSpecAsync(spec, tracked: false);
        var dtos = comments.Select(c => MapCommentToDto(c, c.Replies.Count(r => !r.IsDeleted))).ToList();

        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        var result = new PaginatedResultDto<PostCommentDto>(dtos, pageNumber, pageSize, totalPages, totalItems);

        return new ServiceResult(ResultCodeConst.SYS_Success0002, "Thành công", result);
    }

    public async Task<IServiceResult> GetSubCommentsAsync(Guid parentCommentId, int pageNumber, int pageSize)
    {
        var repo = _uow.Repository<PostComment, Guid>();

        // Lấy tất cả các comment có ParentId là parentCommentId
        var countSpec = new BaseSpecification<PostComment>(c => c.ParentId == parentCommentId && !c.IsDeleted);
        var totalItems = await repo.CountAsync(countSpec);

        if (totalItems == 0)
        {
            return new ServiceResult(ResultCodeConst.SYS_Success0002, "Thành công",
                new PaginatedResultDto<PostCommentDto>(Enumerable.Empty<PostCommentDto>(), pageNumber, pageSize, 0, 0));
        }

        var spec = new BaseSpecification<PostComment>(c => c.ParentId == parentCommentId && !c.IsDeleted);
        spec.ApplyPaging(pageSize, (pageNumber - 1) * pageSize);
        spec.AddOrderBy(c => c.CreatedAt);
        spec.ApplyInclude(q => q.Include(c => c.Author).Include(c => c.Replies));

        var comments = await repo.GetAllWithSpecAsync(spec, tracked: false);
        var dtos = comments.Select(c => MapCommentToDto(c, c.Replies.Count(r => !r.IsDeleted))).ToList();

        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        var result = new PaginatedResultDto<PostCommentDto>(dtos, pageNumber, pageSize, totalPages, totalItems);

        return new ServiceResult(ResultCodeConst.SYS_Success0002, "Thành công", result);
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

    public async Task<IServiceResult> GetUserContentAsync(Guid targetUserId, Guid? currentUserId, int pageNumber, int pageSize)
    {
        var postRepo = _uow.Repository<ContentPost, Guid>();
        var reelRepo = _uow.Repository<Reel, Guid>();

        var user = await _uow.Repository<User, Guid>().GetByIdAsync(targetUserId);
        if (user == null) return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Người dùng không tồn tại.");

        var authorDto = new PostAuthorDto(user.Id, user.FullName ?? "Unknown", user.Avatar);

        // 1. Unified query for IDs and Types only for efficient DB-side pagination
        var postBase = postRepo.GetQueryable(tracked: false)
            .Where(p => p.AuthorId == targetUserId && p.Type == PostType.Community && p.IsPublished)
            .Select(p => new { Id = p.Id, Type = "Post", CreatedAt = p.CreatedAt });

        var reelBase = reelRepo.GetQueryable(tracked: false)
            .Where(r => r.UserId == targetUserId && !r.IsHidden)
            .Select(r => new { Id = r.Id, Type = "Reel", CreatedAt = r.CreatedAt });

        var combinedQuery = postBase.Union(reelBase);
        var totalItems = await combinedQuery.CountAsync();

        if (totalItems == 0)
        {
            return new ServiceResult(ResultCodeConst.SYS_Success0002, "Thành công",
                new PaginatedResultDto<UnifiedContentDto>(Enumerable.Empty<UnifiedContentDto>(), pageNumber, pageSize, 0, 0));
        }

        // Apply paging on the combined set
        var pagedResults = await combinedQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var postIds = pagedResults.Where(x => x.Type == "Post").Select(x => x.Id).ToList();
        var reelIds = pagedResults.Where(x => x.Type == "Reel").Select(x => x.Id).ToList();

        // 2. Fetch full details for the specific IDs found in this page
        var postsData = await postRepo.GetQueryable(tracked: false)
            .Include(p => p.Medias)
            .Include(p => p.Likes)
            .Include(p => p.SharedPost!).ThenInclude(sp => sp.Author)
            .Include(p => p.SharedPost!).ThenInclude(sp => sp.Medias)
            .Include(p => p.SharedReel!).ThenInclude(sr => sr.User)
            .Include(p => p.SharedReel!).ThenInclude(sr => sr.Likes)
            .Where(p => postIds.Contains(p.Id))
            .ToListAsync();

        var reelsData = await reelRepo.GetQueryable(tracked: false)
            .Include(r => r.Likes)
            .Where(r => reelIds.Contains(r.Id))
            .ToListAsync();

        // 3. Map back to DTO while preserving the interleaved order
        var finalItems = new List<UnifiedContentDto>();
        foreach (var res in pagedResults)
        {
            if (res.Type == "Post")
            {
                var p = postsData.FirstOrDefault(x => x.Id == res.Id);
                if (p != null)
                {
                    finalItems.Add(new UnifiedContentDto(
                        p.Id,
                        "Post",
                        authorDto,
                        p.BodyContent,
                        p.Medias.OrderBy(m => m.Order).Select(m => m.Url).ToList(),
                        p.LikeCount,
                        p.CommentCount,
                        currentUserId.HasValue && p.Likes.Any(l => l.UserId == currentUserId.Value),
                        p.CreatedAt,
                        p.SharedPost != null ? MapToDto(p.SharedPost, currentUserId, true) : null,
                        p.SharedReel != null ? new SFARS.Application.Dtos.Reels.ReelResponseDto
                        {
                            Id = p.SharedReel.Id,
                            UserId = p.SharedReel.UserId,
                            UserFullName = p.SharedReel.User?.FullName ?? "Unknown",
                            UserAvatar = p.SharedReel.User?.Avatar,
                            VideoUrl = p.SharedReel.VideoUrl,
                            Caption = p.SharedReel.Caption,
                            CreatedAt = p.SharedReel.CreatedAt,
                            LikeCount = p.SharedReel.LikeCount,
                            CommentCount = p.SharedReel.CommentCount,
                            IsLikedByCurrentUser = currentUserId.HasValue && p.SharedReel.Likes.Any(l => l.UserId == currentUserId.Value)
                        } : null
                    ));
                }
            }
            else
            {
                var r = reelsData.FirstOrDefault(x => x.Id == res.Id);
                if (r != null)
                {
                    finalItems.Add(new UnifiedContentDto(
                        r.Id,
                        "Reel",
                        authorDto,
                        r.Caption,
                        new List<string> { r.VideoUrl },
                        r.LikeCount,
                        r.CommentCount,
                        currentUserId.HasValue && r.Likes.Any(l => l.UserId == currentUserId.Value),
                        r.CreatedAt
                    ));
                }
            }
        }

        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        var result = new PaginatedResultDto<UnifiedContentDto>(finalItems, pageNumber, pageSize, totalPages, totalItems);

        return new ServiceResult(ResultCodeConst.SYS_Success0002, "Thành công", result);
    }

    public async Task<IServiceResult> SharePostAsync(Guid postId, Guid userId, string? content)
    {
        var repo = _uow.Repository<ContentPost, Guid>();
        var original = await repo.GetByIdAsync(postId);
        if (original == null) return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Bài đăng không tồn tại.");

        // 1. Create Share Log
        var shareLog = new ShareLog
        {
            UserId = userId,
            PostId = postId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
        await _uow.Repository<ShareLog, Guid>().AddAsync(shareLog);

        // 2. Increment ShareCount
        original.ShareCount++;

        // 3. Create a NEW Post (SharedContent type)
        var sharedPost = new ContentPost
        {
            Id = Guid.NewGuid(),
            AuthorId = userId,
            Type = PostType.SharedContent,
            BodyContent = content, // Personal message when sharing
            SharedPostId = postId,
            IsPublished = true,
            Title = $"Shared post from {original.Id}",
            Slug = "share-p-" + Guid.NewGuid().ToString("N")[..10],
            CreatedAt = DateTime.UtcNow
        };
        await repo.AddAsync(sharedPost);

        await _uow.SaveChangesAsync();

        // 4. Notify author
        await _notificationService.NotifyShareAsync(original.AuthorId, userId, postId, false);

        return new ServiceResult(ResultCodeConst.SYS_Success0002, "Đã chia sẻ bài viết thành công.");
    }

    private static CommunityPostDto MapToDto(ContentPost p, Guid? viewerId, bool limitMedia = false)
    {
        var mediaList = p.Medias.OrderBy(m => m.Order).AsEnumerable();
        if (limitMedia) mediaList = mediaList.Take(4);

        return new(
            p.Id,
            new PostAuthorDto(p.Author.Id, p.Author.FullName ?? "Unknown", p.Author.Avatar),
            p.BodyContent,
            mediaList.Select(m => new PostMediaDto(m.Id, m.Url, m.ContentType, m.Order)).ToList(),
            p.LikeCount,
            p.CommentCount,
            viewerId.HasValue && p.Likes.Any(l => l.UserId == viewerId.Value),
            p.CreatedAt,
            // Map SharedPost (one level deep is enough)
            p.SharedPost != null ? MapToDto(p.SharedPost, viewerId, true) : null,
            // Map SharedReel
            p.SharedReel != null ? new SFARS.Application.Dtos.Reels.ReelResponseDto
            {
                Id = p.SharedReel.Id,
                UserId = p.SharedReel.UserId,
                UserFullName = p.SharedReel.User?.FullName ?? "Unknown",
                UserAvatar = p.SharedReel.User?.Avatar,
                VideoUrl = p.SharedReel.VideoUrl,
                Caption = p.SharedReel.Caption,
                CreatedAt = p.SharedReel.CreatedAt,
                LikeCount = p.SharedReel.LikeCount,
                CommentCount = p.SharedReel.CommentCount,
                IsLikedByCurrentUser = viewerId.HasValue && p.SharedReel.Likes.Any(l => l.UserId == viewerId.Value)
            } : null
        );
    }

    private static PostCommentDto MapCommentToDto(PostComment c, int totalReplies) => new(
        c.Id,
        c.PostId,
        new PostAuthorDto(c.Author.Id, c.Author.FullName ?? "Unknown", c.Author.Avatar),
        c.IsDeleted ? "[Đã xóa]" : c.Content,
        c.ParentId,
        c.CreatedAt,
        totalReplies
    );

    public async Task<IServiceResult> AdminHidePostAsync(Guid postId, Guid adminId, string reason)
    {
        var post = await _uow.Repository<ContentPost, Guid>().GetByIdAsync(postId);
        if (post is null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Bài đăng không tồn tại.");

        post.IsHiddenByAdmin = true;
        post.AdminNote = reason;
        post.UpdatedAt = DateTime.UtcNow;
        post.UpdatedBy = adminId;

        _uow.Repository<ContentPost, Guid>().Update(post);
        await _uow.SaveChangesAsync();

        // Gửi thông báo cho tác giả (Author)
        await _notificationService.SendNotificationAsync(post.AuthorId, "Nội dung bị ẩn", "Bài viết của bạn đã bị ẩn bởi quản trị viên do: " + reason, NotificationType.System, post.Id);

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Ẩn bài đăng thành công (Admin)", true);
    }

    public async Task<IServiceResult> AdminUnhidePostAsync(Guid postId, Guid adminId)
    {
        var post = await _uow.Repository<ContentPost, Guid>().GetByIdAsync(postId);
        if (post is null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Bài đăng không tồn tại.");

        post.IsHiddenByAdmin = false;
        post.AdminNote = null;
        post.UpdatedAt = DateTime.UtcNow;
        post.UpdatedBy = adminId;

        _uow.Repository<ContentPost, Guid>().Update(post);
        await _uow.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Bỏ ẩn bài bài đăng thành công (Admin)", true);
    }

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
