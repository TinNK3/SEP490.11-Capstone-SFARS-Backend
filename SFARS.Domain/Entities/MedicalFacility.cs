using NetTopologySuite.Geometries;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class MedicalFacility : BaseEntity
{
    public string Name { get; set; } = null!;
    public FacilityType Type { get; set; }
    public string? Address { get; set; }
    public Point Location { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? OperatingHours { get; set; }
    public bool IsActive { get; set; } = true;
}