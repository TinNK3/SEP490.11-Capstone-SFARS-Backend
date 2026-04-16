using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class RescueMission : BaseEntity
{
    public Guid IncidentId { get; set; }
    public Guid RescuerId { get; set; }
    public RescueStatus Status { get; set; } = RescueStatus.Accepted;
    
    public DateTime? StartedAt { get; set; }
    public DateTime? ArrivedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// The distance between rescuer and victim at the exact moment of mission acceptance.
    /// Used as a baseline for Progress-based Claim Timeout validation.
    /// </summary>
    public double? InitialDistanceMeters { get; set; }
    
    public string? RescuerNotes { get; set; }
    public string? PatientConditionAtHandover { get; set; }

    /// <summary>
    /// The timestamp when the next watchdog check is due.
    /// Used to prevent multiple concurrent watchdog jobs for the same mission.
    /// </summary>
    public DateTime? NextCheckAt { get; set; }

    /// <summary>
    /// The distance to the victim captured at the last watchdog check.
    /// Used to calculate macro-progress over the check interval.
    /// </summary>
    public double? LastCheckedDistanceMeters { get; set; }

    public virtual Incident Incident { get; set; } = null!;
    public virtual User Rescuer { get; set; } = null!;
    public virtual ICollection<RescueTrackingLog> TrackingLogs { get; set; } = new List<RescueTrackingLog>();
    public virtual Review? Review { get; set; }
}