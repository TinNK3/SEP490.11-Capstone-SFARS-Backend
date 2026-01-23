using System.ComponentModel.DataAnnotations;
using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Entities;

public class IncidentStatusHistory
{
    [Key]
    public long Id { get; set; }
    public Guid IncidentId { get; set; }
    public IncidentStatus? StatusFrom { get; set; }
    public IncidentStatus StatusTo { get; set; }
    public Guid ChangedBy { get; set; }
    public string? ChangeReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Incident Incident { get; set; } = null!;
}