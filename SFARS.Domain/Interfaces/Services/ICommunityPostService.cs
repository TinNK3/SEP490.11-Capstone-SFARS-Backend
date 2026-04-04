using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;
using System.IO;

namespace SFARS.Domain.Interfaces.Services;

public record MediaUploadInfo(Stream Stream, string FileName, string ContentType);

public interface ICommunityPostService
{
    Task<IServiceResult> GetPostsAsync(CommunityPostSpecParams specParams, Guid? currentUserId);
    Task<IServiceResult> GetPostByIdAsync(Guid postId, Guid? currentUserId);
    Task<IServiceResult> CreatePostAsync(Guid authorId, string? content, List<MediaUploadInfo>? mediaFiles);
    Task<IServiceResult> UpdatePostAsync(Guid postId, Guid authorId, string? content, List<string>? retainedMediaUrls, List<MediaUploadInfo>? newMediaFiles);
    Task<IServiceResult> HidePostAsync(Guid postId, Guid requesterId);
    Task<IServiceResult> UnhidePostAsync(Guid postId, Guid requesterId);
    Task<IServiceResult> DeletePostAsync(Guid postId, Guid requesterId);
    Task<IServiceResult> ToggleLikeAsync(Guid postId, Guid userId);
    Task<IServiceResult> AddCommentAsync(Guid postId, Guid authorId, string content, Guid? parentId);
    Task<IServiceResult> GetCommentsAsync(Guid postId, int pageNumber, int pageSize);
    Task<IServiceResult> GetSubCommentsAsync(Guid parentCommentId, int pageNumber, int pageSize);
    Task<IServiceResult> DeleteCommentAsync(Guid commentId, Guid requesterId);
}
