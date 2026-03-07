using NetTopologySuite.Geometries;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class MedicalFacility : BaseEntity
{
    public string Name { get; set; } = null!;
    public FacilityType Type { get; set; }
    public string? Address { get; set; }
    public string? Province { get; set; }
    public Point Location { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public TimeOnly? OpenHours { get; set; }
    public TimeOnly? CloseHours { get; set; }
    public bool EmergencyAvailable { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public bool HasAntivenom { get; set; } = false;
    public DateTime? AntivenomUpdatedAt { get; set; }
    public string? Notes { get; set; }
}