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
    
    public string? TotalCompletionTime
    {
        get
        {
            if (StartedAt.HasValue && CompletedAt.HasValue)
            {
                var diff = CompletedAt.Value - StartedAt.Value;
                return $"{(int)diff.TotalHours:D2}:{diff.Minutes:D2}:{diff.Seconds:D2}";
            }
            return null;
        }
    }

    // Linked incident summary
    public IncidentDto? Incident { get; set; }
}