using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class ReelComment : BaseEntity
{
    public Guid ReelId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ParentCommentId { get; set; }
    public string Content { get; set; } = null!;

    public virtual Reel Reel { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual ReelComment? ParentComment { get; set; }
    public virtual ICollection<ReelComment> SubComments { get; set; } = new List<ReelComment>();
}
