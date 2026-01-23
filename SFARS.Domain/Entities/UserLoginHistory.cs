using System.ComponentModel.DataAnnotations;

namespace SFARS.Domain.Entities;

public class UserLoginHistory
{
    [Key]
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime LoginAt { get; set; } = DateTime.UtcNow;
    public string? IPAddress { get; set; }
    public string? UserAgent { get; set; }

    public virtual User User { get; set; } = null!;
}