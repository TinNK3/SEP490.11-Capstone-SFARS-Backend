using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Incident;

/// <summary>
/// DTO for incident media (photos/videos)
/// </summary>
public class IncidentMediaDto
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public string MediaUrl { get; set; } = null!;
    public MediaType MediaType { get; set; }
    public DateTime CreatedAt { get; set; }
}