using SFARS.Domain.Common.Enum;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Application.Interfaces.Services;

public interface INotificationService
{
    Task<IServiceResult> SendNotificationAsync(Guid userId, string title, string message, NotificationType type, Guid? referenceId = null);
    Task<IServiceResult> SendNotificationsAsync(IEnumerable<Guid> userIds, string title, string message, NotificationType type, Guid? referenceId = null);
    Task<IServiceResult> GetNotificationsAsync(Guid userId, NotificationSpecParams specParams);
    Task<IServiceResult> MarkAsReadAsync(Guid notificationId, Guid userId);
    Task<IServiceResult> MarkAllAsReadAsync(Guid userId);
    Task<IServiceResult> GetUnreadCountAsync(Guid userId);

    // Social Notifications
    Task NotifyLikeAsync(Guid authorId, Guid likerId, Guid contentId, bool isReel);
    Task NotifyCommentAsync(Guid authorId, Guid commenterId, Guid contentId, bool isReel);
    Task NotifyCommentReplyAsync(Guid parentCommentAuthorId, Guid replierId, Guid contentId, bool isReel);
    Task NotifyShareAsync(Guid authorId, Guid sharerId, Guid contentId, bool isReel);
}
