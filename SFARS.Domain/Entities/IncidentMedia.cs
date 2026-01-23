using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class IncidentMedia : BaseEntity
{
    public Guid IncidentId { get; set; }
    public string MediaUrl { get; set; } = null!;
    public MediaType MediaType { get; set; }

    public virtual Incident Incident { get; set; } = null!;
}