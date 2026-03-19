using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class ContentPost : BaseEntity
{
    public string Title { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? BodyContent { get; set; }
    public PostType Type { get; set; }
    public string? ThumbnailUrl { get; set; }
    public Guid AuthorId { get; set; }
    public bool IsPublished { get; set; }

    public int LikeCount { get; set; }
    public int CommentCount { get; set; }

    public virtual User Author { get; set; } = null!;
    public virtual ICollection<PostMedia> Medias { get; set; } = [];
    public virtual ICollection<PostComment> Comments { get; set; } = [];
    public virtual ICollection<PostLike> Likes { get; set; } = [];
}