using SFARS.Domain.Entities;
using SFARS.Domain.Specifications.Params;
using System.Linq.Expressions;

namespace SFARS.Domain.Specifications.Faqs
{
    /// <summary>
    /// FAQ specifications for query/filter/sort/paging.
    /// </summary>
    public class FaqSpecification : BaseSpecification<Faq>
    {
        private FaqSpecification(Expression<Func<Faq, bool>> criteria)
            : base(criteria) { }

        public static FaqSpecification ActiveOrdered()
        {
            var spec = new FaqSpecification(f => f.IsActive);
            spec.AddOrderBy(f => f.Order);
            return spec;
        }

        public static FaqSpecification List(BaseSpecParams p)
        {
            var spec = new FaqSpecification(_ => true);
            ApplySearchFilter(spec, p.Search);
            spec.AddOrderBy(f => f.Order);
            spec.ApplyPaging(p.GetTake(), p.GetSkip());
            return spec;
        }

        public static FaqSpecification Count(BaseSpecParams p)
        {
            var spec = new FaqSpecification(_ => true);
            ApplySearchFilter(spec, p.Search);
            return spec;
        }

        public static FaqSpecification HighestOrder(bool activeOnly)
        {
            var spec = new FaqSpecification(_ => true);
            if (activeOnly)
            {
                spec.AddFilter(f => f.IsActive);
            }

            spec.AddOrderByDescending(f => f.Order);
            spec.ApplyPaging(1, 0);
            return spec;
        }

        private static void ApplySearchFilter(FaqSpecification spec, string? rawSearch)
        {
            var keyword = rawSearch?.Trim();
            if (string.IsNullOrEmpty(keyword))
                return;

            spec.AddFilter(f => !string.IsNullOrEmpty(f.Question) && f.Question.Contains(keyword));
        }
    }
}
