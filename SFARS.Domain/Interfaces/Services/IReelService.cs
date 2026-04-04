using SFARS.Domain.Interfaces.Services.Base;
using System.IO;

namespace SFARS.Domain.Interfaces.Services;

public interface IReelService
{
    // Feed
    Task<IServiceResult> GetReelsFeedAsync(Guid? currentUserId, int pageNumber, int pageSize);
    Task<IServiceResult> GetReelsByUserIdAsync(Guid? currentUserId, Guid targetUserId, int pageNumber, int pageSize);
    Task<IServiceResult> GetReelByIdAsync(Guid? currentUserId, Guid reelId);

    // CRUD
    Task<IServiceResult> CreateReelAsync(Guid currentUserId, string? caption, Stream videoStream, string fileName, string contentType, long contentLength);
    Task<IServiceResult> HideReelAsync(Guid currentUserId, Guid reelId);
    Task<IServiceResult> DeleteReelAsync(Guid currentUserId, Guid reelId);

    // Likes
    Task<IServiceResult> ToggleLikeAsync(Guid currentUserId, Guid reelId);

    // Comments
    Task<IServiceResult> AddCommentAsync(Guid currentUserId, Guid reelId, string content, Guid? parentCommentId);
    Task<IServiceResult> GetCommentsAsync(Guid reelId, int pageNumber, int pageSize);
    Task<IServiceResult> GetSubCommentsAsync(Guid parentCommentId, int pageNumber, int pageSize);
    Task<IServiceResult> DeleteCommentAsync(Guid currentUserId, Guid commentId);
}
