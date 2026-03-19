using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Rescuer;

public class RescuerProfileDto
{
    // ── User Info ──────────────────────────────────────────────────
    public Guid UserId { get; set; }
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
    public DateTime? AvailableUpdatedAt { get; set; }
}
