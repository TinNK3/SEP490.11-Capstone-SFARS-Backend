using SFARS.Domain.Entities;
using SFARS.Domain.Specifications.Params;
using System.Linq.Expressions;

namespace SFARS.Domain.Specifications.FirstAidDetails
{
    /// <summary>
    /// First Aid Detail specifications for query/filter/sort/paging.
    /// </summary>
    public class FirstAidDetailSpecification : BaseSpecification<FirstAidDetail>
    {
        private FirstAidDetailSpecification(Expression<Func<FirstAidDetail, bool>> criteria)
            : base(criteria) { }

        public static FirstAidDetailSpecification List(BaseSpecParams p)
        {
            var spec = new FirstAidDetailSpecification(_ => true);
            ApplySearchFilter(spec, p.Search);
            spec.AddOrderBy(f => f.StepOrder);
            spec.ApplyPaging(p.GetTake(), p.GetSkip());
            return spec;
        }

        public static FirstAidDetailSpecification Count(BaseSpecParams p)
        {
            var spec = new FirstAidDetailSpecification(_ => true);
            ApplySearchFilter(spec, p.Search);
            return spec;
        }

        private static void ApplySearchFilter(FirstAidDetailSpecification spec, string? rawSearch)
        {
            var keyword = rawSearch?.Trim();
            if (string.IsNullOrEmpty(keyword))
                return;

            spec.AddFilter(e => e.Title.Contains(keyword) ||
                                (e.ContentMarkdown != null && e.ContentMarkdown.Contains(keyword)));
        }
    }
}