using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace SFARS.Domain.Specifications.Interfaces
{
    public interface ISpecification<TEntity>
    {
        Expression<Func<TEntity, bool>> Criteria { get; }
        List<Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>> Includes { get; }
        List<Expression<Func<TEntity, bool>>> Filters { get; }
        Expression<Func<TEntity, object>> OrderBy { get; }
        Expression<Func<TEntity, object>> OrderByDescending { get; }
        Expression<Func<TEntity, object>> GroupBy { get; }
        int Take { get; }
        int Skip { get; }
        bool IsPagingEnabled { get; }
        bool AsSplitQuery { get; }
        bool IsDistinct { get; }
    }
}