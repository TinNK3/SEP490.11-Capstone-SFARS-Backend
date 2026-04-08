using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.User;

/// <summary>
/// Detailed DTO for rescuer information displayed when clicking a map marker.
/// Includes real-time calculated distance and ETA.
/// </summary>
public class RescuerDetailDto
{
    // Basic Info
    public string FullName { get; set; } = null!;
    public string? Avatar { get; set; }
    public string? Phone { get; set; }
    public Gender? Gender { get; set; }
    public string? Address { get; set; }

    // Professional Info
    public int ExperienceYears { get; set; }
    public string? LicensePlate { get; set; }
    public VehicleType VehicleType { get; set; }
    public int TotalMissions { get; set; }

    // Real-time Context
    public double DistanceKM { get; set; }
    public int EtaMinutes { get; set; }
    public DateTime? LocationUpdatedAt { get; set; }
}