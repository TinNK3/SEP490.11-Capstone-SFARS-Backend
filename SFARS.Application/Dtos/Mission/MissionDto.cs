using SFARS.Domain.Common.Enum;
using SFARS.Application.Dtos.Incident;

namespace SFARS.Application.Dtos.Mission;

public class MissionDto
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public RescueStatus Status { get; set; }
    
    public DateTime? StartedAt { get; set; }
    public DateTime? ArrivedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public string? RescuerNotes { get; set; }
    public string? PatientConditionAtHandover { get; set; }

    // Linked incident summary
    public IncidentDto? Incident { get; set; }
}