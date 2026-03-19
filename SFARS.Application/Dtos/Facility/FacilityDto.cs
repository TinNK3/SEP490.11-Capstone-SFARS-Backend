using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Facility;

/// <summary>
/// Data Transfer Object for MedicalFacility entity.
/// Used for Create, Update operations, service layer data transfer, and API responses.
/// </summary>
public class FacilityDto
{
    public Guid Id { get; set; }
    
    // Basic Info
    public string Name { get; set; } = null!;
    public FacilityType FacilityType { get; set; }
    
    // Location
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    
    // Address
    public string? Address { get; set; }
    public string? Province { get; set; }
    
    // Contact
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    
    // Operating Hours
    public TimeOnly? OpenHours { get; set; }
    public TimeOnly? CloseHours { get; set; }
    
    // Capabilities
    public bool EmergencyAvailable { get; set; }
    public bool HasAntivenom { get; set; }
    public DateTime? AntivenomUpdatedAt { get; set; }
    
    /// <summary>
    /// True if HasAntivenom is true but AntivenomUpdatedAt is more than 30 days ago.
    /// </summary>
    public bool IsAntivenomStale { get; set; }
    
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>
    /// Distance in meters (populated for nearby search results).
    /// </summary>
    public double? DistanceMeters { get; set; }
}