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
    public Guid? CurrentAiInferenceId { get; set; }
    public virtual AiInference? CurrentAiInference { get; set; }
    
    // AI Review Snapshots
    public AiReviewStatus? CurrentAiReviewStatus { get; set; }
    public Guid? CurrentAiReviewId { get; set; }
    public Guid? HumanReviewedSnakeId { get; set; }
    public ToxinGroup? HumanReviewedToxinGroup { get; set; }

    public virtual User Victim { get; set; } = null!;
    public virtual Snake? Snake { get; set; }
    public virtual ICollection<IncidentStatusHistory> StatusHistories { get; set; } = new List<IncidentStatusHistory>();
    public virtual ICollection<IncidentMedia> Medias { get; set; } = new List<IncidentMedia>();
    public virtual ICollection<RescueMission> Missions { get; set; } = new List<RescueMission>();
    public virtual ICollection<AiInference> AiInferences { get; set; } = new List<AiInference>();
    public virtual ICollection<IncidentSymptom> Symptoms { get; set; } = new List<IncidentSymptom>();
    public virtual IncidentChat? Chat { get; set; }

    // Public tracking (QR code sharing)
    public string? TrackingCode { get; set; }
    public DateTime? TrackingCodeExpiresAt { get; set; }

    /// <summary>
    /// Set by AnalyzeAsync when AI results are returned to the user.
    /// Defines the server-authoritative end of the soft-cancel grace period.
    /// Null until Analyze is called; cleared after the grace period expires or incident is dispatched.
    /// </summary>
    public DateTime? GraceExpiresAt { get; set; }

    /// <summary>
    /// JSON array of Hangfire job IDs scheduled for this incident's dispatch chain.
    /// Format: ["jobId1", "jobId2", "jobId3", "jobId4"]
    /// Used by CancelIncidentAsync to delete pending dispatch jobs if victim cancels during grace period.
    /// </summary>
    public string? DispatchJobIds { get; set; }
}