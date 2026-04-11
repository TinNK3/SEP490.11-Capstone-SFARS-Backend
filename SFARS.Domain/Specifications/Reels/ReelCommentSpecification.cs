using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Entities;
using System.Linq.Expressions;

namespace SFARS.Domain.Specifications.Reels
{
    public class ReelCommentSpecification : BaseSpecification<ReelComment>
    {
        public ReelCommentSpecification(Expression<Func<ReelComment, bool>> criteria) : base(criteria) { }

        public static ReelCommentSpecification ParentComments(Guid reelId, int skip, int take)
        {
            var spec = new ReelCommentSpecification(c => c.ReelId == reelId && c.ParentCommentId == null && !c.IsDeleted);
            spec.AddOrderByDescending(c => c.CreatedAt);
            spec.ApplyPaging(take, skip);
            spec.ApplyInclude(q => q.Include(c => c.User).Include(c => c.SubComments));
            return spec;
        }

        public static ReelCommentSpecification SubComments(Guid parentCommentId, int skip, int take)
        {
            var spec = new ReelCommentSpecification(c => c.ParentCommentId == parentCommentId && !c.IsDeleted);
            spec.AddOrderBy(c => c.CreatedAt);
            spec.ApplyPaging(take, skip);
            spec.ApplyInclude(q => q.Include(c => c.User));
            return spec;
        }

        public static ReelCommentSpecification ForCount(Guid reelId)
        {
            return new ReelCommentSpecification(c => c.ReelId == reelId && c.ParentCommentId == null && !c.IsDeleted);
        }

        public static ReelCommentSpecification ForSubCount(Guid parentCommentId)
        {
            return new ReelCommentSpecification(c => c.ParentCommentId == parentCommentId && !c.IsDeleted);
        }
    }
}
