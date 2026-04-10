using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Specifications.Params;

public class MissionSpecParams : BaseSpecParams
{
    public RescueStatus? Status { get; set; }
}