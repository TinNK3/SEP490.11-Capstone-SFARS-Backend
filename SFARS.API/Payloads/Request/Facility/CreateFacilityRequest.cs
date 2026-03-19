using SFARS.Domain.Common.Enum;

namespace SFARS.API.Payloads.Request.Facility;

/// <summary>
/// Request model for creating a new medical facility (API input contract).
/// </summary>
public class CreateFacilityRequest
{
    public string Name { get; set; } = null!;
    public FacilityType FacilityType { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Address { get; set; }
    public string? Province { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public TimeOnly? OpenHours { get; set; }
    public TimeOnly? CloseHours { get; set; }
    public bool EmergencyAvailable { get; set; }
    public bool HasAntivenom { get; set; }
    public string? Notes { get; set; }
}

