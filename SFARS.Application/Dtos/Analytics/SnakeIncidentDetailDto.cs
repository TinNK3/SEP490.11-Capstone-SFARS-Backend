using System;

namespace SFARS.Application.Dtos.Analytics;

public class SnakeIncidentDetailDto
{
    // ===== INCIDENT DETAILS =====
    /// <summary>Unique incident identifier</summary>
    public Guid IncidentId { get; set; }

    /// <summary>Incident code/reference (e.g., "DEMO-2026-012")</summary>
    public string IncidentCode { get; set; } = null!;

    /// <summary>When the incident was created (UTC)</summary>
    public DateTime IncidentDate { get; set; }

    /// <summary>Incident location with latitude/longitude</summary>
    public SnakeIncidentLocationDto IncidentLocation { get; set; } = null!;

    /// <summary>Severity level (Critical, High, Medium, Low)</summary>
    public string Severity { get; set; } = null!;

    /// <summary>Description of the incident (nullable)</summary>
    public string? IncidentDescription { get; set; }

    // ===== VICTIM DETAILS =====
    /// <summary>Unique identifier of the person who reported the incident</summary>
    public Guid VictimId { get; set; }

    /// <summary>Full name of the victim</summary>
    public string VictimName { get; set; } = null!;

    /// <summary>Contact phone number of victim (nullable)</summary>
    public string? VictimPhone { get; set; }

    /// <summary>Address of victim (nullable)</summary>
    public string? VictimAddress { get; set; }

    // ===== AI IDENTIFICATION =====
    /// <summary>Details of AI snake identification</summary>
    public SnakeAiIdentificationDto AiIdentification { get; set; } = null!;

    // ===== TOP CANDIDATE SNAKES =====
    /// <summary>List of all top candidate snakes considered by AI (sorted by confidence)</summary>
    public List<AiCandidateSnakeDto> TopCandidates { get; set; } = new();

    // ===== RESCUER REVIEW =====
    /// <summary>
    /// Rescuer's review/correction of the AI identification (nullable).
    /// May be null if no rescuer has reviewed this incident yet.
    /// </summary>
    public SnakeRescuerReviewDto? RescuerReview { get; set; }

    // ===== RESCUE MISSION =====
    /// <summary>
    /// Details of the rescue mission assigned to this incident (nullable).
    /// May be null if no rescuer has been assigned yet.
    /// </summary>
    public SnakeMissionDetailDto? Mission { get; set; }
}
