using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Notification;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; }
    public Guid? ReferenceId { get; set; }
}
