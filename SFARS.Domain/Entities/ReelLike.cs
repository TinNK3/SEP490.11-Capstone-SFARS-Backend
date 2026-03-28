using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class ReelLike
{
    public Guid ReelId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Reel Reel { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
