using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Specifications;

public class CommunityIncidentSpecification : BaseSpecification<Incident>
{
    public CommunityIncidentSpecification(BaseSpecParams specParams, bool isCount = false)
        : base(x => 
            x.CurrentStatus == IncidentStatus.Assigned ||
            x.CurrentStatus == IncidentStatus.EnRoute ||
            x.CurrentStatus == IncidentStatus.Arrived ||
            x.CurrentStatus == IncidentStatus.Closed)
    {
        if (!isCount)
        {
            if (!string.IsNullOrEmpty(specParams.Sort))
            {
                switch (specParams.Sort)
                {
                    case "createdAtAsc":
                        AddOrderBy(i => i.CreatedAt);
                        break;
                    default:
                        AddOrderByDescending(i => i.CreatedAt);
                        break;
                }
            }
            else
            {
                AddOrderByDescending(i => i.CreatedAt);
            }

            ApplyPaging(specParams.GetTake(), specParams.GetSkip());
        }
    }
}