using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class PointTransaction : BaseEntity
{
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public PointActivityType ActivityType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Description { get; set; }

    public virtual User User { get; set; } = null!;
}