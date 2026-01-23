using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class SnakeImage : BaseEntity
{
    public Guid SnakeId { get; set; }
    public string ImageUrl { get; set; } = null!;
    public bool IsPrimary { get; set; }

    public virtual Snake Snake { get; set; } = null!;
}