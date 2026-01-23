using SFARS.Domain.Common.Enum;
using System.ComponentModel.DataAnnotations;

namespace SFARS.Domain.Entities;

public class NotificationLog
{
    [Key]
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; } = null!;
}