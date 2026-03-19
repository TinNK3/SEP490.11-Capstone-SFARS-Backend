using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class PostComment : BaseEntity
{
    public Guid PostId { get; set; }
    public Guid AuthorId { get; set; }
    public string Content { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public bool IsDeleted { get; set; }

    public virtual ContentPost Post { get; set; } = null!;
    public virtual User Author { get; set; } = null!;
    public virtual PostComment? Parent { get; set; }
    public virtual ICollection<PostComment> Replies { get; set; } = [];
}
