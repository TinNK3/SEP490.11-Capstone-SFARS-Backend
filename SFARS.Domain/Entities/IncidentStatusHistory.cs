using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class IncidentStatusHistory : BaseEntity
{
    public Guid IncidentId { get; set; }
    public IncidentStatus? StatusFrom { get; set; }
    public IncidentStatus StatusTo { get; set; }
    public Guid ChangedBy { get; set; }
    public string? ChangeReason { get; set; }

    public virtual Incident Incident { get; set; } = null!;
}