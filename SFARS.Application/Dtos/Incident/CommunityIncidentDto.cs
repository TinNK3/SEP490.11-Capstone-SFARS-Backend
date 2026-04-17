using SFARS.Domain.Common.Enum;
using SFARS.Application.Dtos.AiInference;

namespace SFARS.Application.Dtos.Incident;

public class CommunityIncidentDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    
    // Masked coordinates (rounded to 3 decimal places for general area, ~110 meters accuracy)
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public string? AddressString { get; set; }
    public IncidentStatus CurrentStatus { get; set; }
    public SeverityLevel PriorityLevel { get; set; }
    
    public DateTime CreatedAt { get; set; }

    // Display incident image (Snake or Wound)
    public string? IncidentImage { get; set; }
}