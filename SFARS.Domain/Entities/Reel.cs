using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class Reel : BaseEntity
{
    public Guid UserId { get; set; }
    public string VideoUrl { get; set; } = null!;
    public string CloudinaryPublicId { get; set; } = null!;
    public string? Caption { get; set; }
    public bool IsHidden { get; set; } = false;

    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int ShareCount { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual ICollection<ReelLike> Likes { get; set; } = new List<ReelLike>();
    public virtual ICollection<ReelComment> Comments { get; set; } = new List<ReelComment>();
}
