using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class Review : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid ReviewerId { get; set; }
    public Guid? TargetId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }

    public virtual RescueMission Mission { get; set; } = null!;
    public virtual User Reviewer { get; set; } = null!;
    public virtual User? Target { get; set; }
}