using NetTopologySuite.Geometries;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class Incident : BaseEntity
{
    public string Code { get; set; } = null!; // SOS-2023-001
    public Guid VictimId { get; set; }
    public Guid? SnakeId { get; set; }
    public Point Location { get; set; } = null!;
    public string? AddressString { get; set; }
    public string? Description { get; set; }
    
    // Status
    public IncidentStatus CurrentStatus { get; set; } = IncidentStatus.Pending;
    public SeverityLevel PriorityLevel { get; set; } = SeverityLevel.Low;
    
    // AI
    public string? AiPredictionResult { get; set; }
    public double? AiConfidenceScore { get; set; }

    public virtual User Victim { get; set; } = null!;
    public virtual Snake? Snake { get; set; }
    public virtual ICollection<IncidentStatusHistory> StatusHistories { get; set; } = new List<IncidentStatusHistory>();
    public virtual ICollection<IncidentMedia> Medias { get; set; } = new List<IncidentMedia>();
    public virtual ICollection<RescueMission> Missions { get; set; } = new List<RescueMission>();
}