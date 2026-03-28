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
}
