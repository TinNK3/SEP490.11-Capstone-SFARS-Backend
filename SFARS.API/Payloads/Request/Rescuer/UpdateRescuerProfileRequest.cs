using SFARS.Domain.Common.Enum;

namespace SFARS.API.Payloads.Request.Rescuer;

/// <summary>
/// Request payload for updating rescuer's combined profile (user info + rescuer-specific info).
/// </summary>
public class UpdateRescuerProfileRequest
{
    // ── User Info ──────────────────────────────────────────────────
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Avatar { get; set; }
    public string? Address { get; set; }
    public Gender? Gender { get; set; }
    public DateTime? Dob { get; set; }

    // ── Rescuer Profile ────────────────────────────────────────────
    public int ExperienceYears { get; set; }
    public VehicleType VehicleType { get; set; }
    public string? LicensePlate { get; set; }
    public double CoverageRadiusKM { get; set; }
    public bool IsAvailable { get; set; }
}