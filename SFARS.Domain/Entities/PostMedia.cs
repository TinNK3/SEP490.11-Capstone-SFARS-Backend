using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class PostMedia : BaseEntity
{
    public Guid PostId { get; set; }
    public string Url { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public int Order { get; set; }

    public virtual ContentPost Post { get; set; } = null!;
}
