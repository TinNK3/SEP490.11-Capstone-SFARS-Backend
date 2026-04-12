using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using System.Linq.Expressions;

namespace SFARS.Domain.Specifications.FirstAidDetails
{
    /// <summary>
    /// First Aid Detail specifications for querying procedures.
    /// </summary>
    public class FirstAidDetailSpecification : BaseSpecification<FirstAidDetail>
    {
        private FirstAidDetailSpecification(Expression<Func<FirstAidDetail, bool>> criteria)
            : base(criteria) { }

        public static FirstAidDetailSpecification AllGrouped(ToxinGroup? toxinGroup = null)
        {
            var spec = new FirstAidDetailSpecification(e => true);
            
            if (toxinGroup.HasValue)
            {
                spec.AddFilter(e => e.ToxinGroup == toxinGroup.Value);
            }

            // Ensure they are fetched in the correct order so GroupBy preserves the step order
            spec.AddOrderBy(f => f.ToxinGroup);
            spec.AddOrderBy(f => f.StepOrder);
            return spec;
        }
    }
}