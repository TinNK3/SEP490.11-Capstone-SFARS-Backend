using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Entities;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Specifications;

public class MissionSpecification : BaseSpecification<RescueMission>
{
    public MissionSpecification(MissionSpecParams specParams, Guid rescuerId, bool isCount = false)
        : base(x => 
            x.RescuerId == rescuerId &&
            (!specParams.Status.HasValue || x.Status == specParams.Status))
    {
        if (!isCount)
        {
            ApplyInclude(q => q.Include(m => m.Incident));

            if (!string.IsNullOrEmpty(specParams.Sort))
            {
                switch (specParams.Sort)
                {
                    case "startedAtAsc":
                        AddOrderBy(m => m.StartedAt ?? m.CreatedAt);
                        break;
                    default:
                        AddOrderByDescending(m => m.StartedAt ?? m.CreatedAt);
                        break;
                }
            }
            else
            {
                AddOrderByDescending(m => m.StartedAt ?? m.CreatedAt);
            }

            ApplyPaging(specParams.GetTake(), specParams.GetSkip());
        }
    }
}