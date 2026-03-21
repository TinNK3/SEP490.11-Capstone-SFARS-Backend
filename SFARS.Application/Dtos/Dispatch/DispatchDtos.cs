namespace SFARS.Application.Dtos.Dispatch;

/// <summary>
/// Pushed to rescuers via SignalR on the "sos:dispatch" event.
/// Contains everything a rescuer needs to decide whether to accept.
/// </summary>
public class SosDispatchNotificationDto
{
    // --- Core Routing Data ---
    public Guid IncidentId { get; set; }
    public string IncidentCode { get; set; } = null!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? AddressString { get; set; }
    public double DistanceKm { get; set; }   // From this rescuer's location
    public int EstimatedEtaMin { get; set; }
    public int Tier { get; set; }   // 1 / 2 / 3
    public string PriorityLevel { get; set; } = null!; // Base for Frontend text render

    // --- AI / Context Summary ---
    public string? IncidentImageUrl { get; set; }   // Rich Push Thumbnail
    public bool IsAiSkipped { get; set; }           // True if user skipped photo capture
    public string? AiPrimarySnakeName { get; set; } // Top 1 Identity
    public double? AiConfidence { get; set; }       // e.g. 0.92
    public string? ToxinGroup { get; set; }         // Neurotoxic / unknown...
    
    // --- Voice Extraction ---
    public string? SymptomAudioUrl { get; set; }
    public int? MinutesSinceBite { get; set; }
    public string? ExtractedSymptoms { get; set; }

    public DateTime DispatchedAt { get; set; }
}

/// <summary>
/// Pushed to ALL rescuers in the current tier when one has already accepted.
/// FE should dismiss the SOS card for this incident.
/// </summary>
public class SosAssignedNotificationDto
{
    public Guid IncidentId { get; set; }
    public string IncidentCode { get; set; } = null!;
}

/// <summary>
/// Pushed to the victim on the "sos:fallback" event after all tiers are exhausted.
/// FE shows the critical fallback UI (115 button, nearest hospital, first-aid card).
/// </summary>
public class SosFallbackDto
{
    public Guid IncidentId { get; set; }
    public string Message { get; set; } = null!;
}