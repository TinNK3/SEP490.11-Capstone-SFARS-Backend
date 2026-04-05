using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Incident;

public class IncidentStatusHistoryDto
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public IncidentStatus? StatusFrom { get; set; }
    public IncidentStatus StatusTo { get; set; }
    public string? ChangeReason { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public Guid ChangedBy { get; set; }
    public string ChangedByName { get; set; } = null!;
}