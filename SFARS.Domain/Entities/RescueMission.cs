using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class RescueMission : BaseEntity
{
    public Guid IncidentId { get; set; }
    public Guid RescuerId { get; set; }
    public RescueStatus Status { get; set; } = RescueStatus.Pending;
    
    public DateTime? StartedAt { get; set; }
    public DateTime? ArrivedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public string? RescuerNotes { get; set; }
    public string? PatientConditionAtHandover { get; set; }

    public virtual Incident Incident { get; set; } = null!;
    public virtual User Rescuer { get; set; } = null!;
    public virtual ICollection<RescueTrackingLog> TrackingLogs { get; set; } = new List<RescueTrackingLog>();
    public virtual Review? Review { get; set; }
}