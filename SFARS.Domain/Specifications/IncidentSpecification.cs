using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Entities;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Specifications;

public class IncidentSpecification : BaseSpecification<Incident>
{
    /// <summary>
    /// Gets incidents, filtering by user if userId is provided.
    /// </summary>
    public IncidentSpecification(IncidentSpecParams specParams, Guid? userId = null, bool isCount = false)
        : base(x => 
            (!userId.HasValue || x.VictimId == userId.Value) &&
            (!specParams.Status.HasValue || x.CurrentStatus == specParams.Status) &&
            (!specParams.Priority.HasValue || x.PriorityLevel == specParams.Priority) &&
            (string.IsNullOrEmpty(specParams.Search) || x.Code.ToLower().Contains(specParams.Search)))
    {
        if (!isCount)
        {
            ApplyInclude(q => q.Include(i => i.Victim));

            if (!string.IsNullOrEmpty(specParams.Sort))
            {
                switch (specParams.Sort)
                {
                    case "createdAtAsc":
                        AddOrderBy(i => i.CreatedAt);
                        break;
                    case "priorityDesc":
                        AddOrderByDescending(i => i.PriorityLevel);
                        break;
                    case "priorityAsc":
                        AddOrderBy(i => i.PriorityLevel);
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