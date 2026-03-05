using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Incident;

/// <summary>
/// Full tracking data for authenticated incident participants.
/// </summary>
public class IncidentTrackingDto
{
    public Guid IncidentId { get; set; }
    public string IncidentCode { get; set; } = null!;
    public string? TrackingCode { get; set; }
    public IncidentStatus Status { get; set; }
    public double IncidentLatitude { get; set; }
    public double IncidentLongitude { get; set; }
    public List<TrackingParticipantDto> Participants { get; set; } = new();
}

/// <summary>
/// Participant location data for authenticated tracking.
/// </summary>
public class TrackingParticipantDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = null!;
    public string Role { get; set; } = null!; // "User" or "Rescuer"
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? LocationUpdatedAt { get; set; }
    public double? AccuracyMeters { get; set; }
    public string AccuracyLevel { get; set; } = "unknown";
}

/// <summary>
/// Minimized tracking DTO for public QR sharing — no userId, no userName.
/// </summary>
public class PublicIncidentTrackingDto
{
    public string IncidentCode { get; set; } = null!;
    public string Status { get; set; } = null!;
    public double IncidentLatitude { get; set; }
    public double IncidentLongitude { get; set; }
    public List<PublicTrackingParticipantDto> Participants { get; set; } = new();
}

/// <summary>
/// Minimized participant DTO for public tracking — data minimization.
/// </summary>
public class PublicTrackingParticipantDto
{
    public string Role { get; set; } = null!;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? LocationUpdatedAt { get; set; }
    public string AccuracyLevel { get; set; } = "unknown";
}