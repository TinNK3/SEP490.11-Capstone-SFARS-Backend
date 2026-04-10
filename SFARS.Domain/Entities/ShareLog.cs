using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class ShareLog : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? PostId { get; set; }
    public Guid? ReelId { get; set; }
    public string? Content { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual ContentPost? Post { get; set; }
    public virtual Reel? Reel { get; set; }
}
