using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Entities;

public class RescuerProfile
{
    [Key, ForeignKey("User")]
    public Guid UserId { get; set; }

    public int ExperienceYears { get; set; }
    public VehicleType VehicleType { get; set; }
    public string? LicensePlate { get; set; }
    public double CoverageRadiusKM { get; set; }
    public bool IsVerified { get; set; }
    public Guid? ApprovedBy { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime? AvailableUpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}