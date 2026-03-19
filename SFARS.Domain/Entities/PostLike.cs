namespace SFARS.Domain.Entities;

public class PostLike
{
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public DateTime LikedAt { get; set; } = DateTime.UtcNow;

    public virtual ContentPost Post { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
