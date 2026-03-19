using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services;

public interface ICommunityPostService
{
    Task<IServiceResult> GetPostsAsync(int page, int pageSize, Guid currentUserId);
    Task<IServiceResult> GetPostByIdAsync(Guid postId, Guid currentUserId);
    Task<IServiceResult> CreatePostAsync(Guid authorId, string? content, List<string>? mediaUrls);
    Task<IServiceResult> DeletePostAsync(Guid postId, Guid requesterId);
    Task<IServiceResult> ToggleLikeAsync(Guid postId, Guid userId);
    Task<IServiceResult> AddCommentAsync(Guid postId, Guid authorId, string content, Guid? parentId);
    Task<IServiceResult> GetCommentsAsync(Guid postId);
    Task<IServiceResult> DeleteCommentAsync(Guid commentId, Guid requesterId);
}
