using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Specifications.Params;

public class IncidentSpecParams : BaseSpecParams
{
    public IncidentStatus? Status { get; set; }
    public SeverityLevel? Priority { get; set; }
}