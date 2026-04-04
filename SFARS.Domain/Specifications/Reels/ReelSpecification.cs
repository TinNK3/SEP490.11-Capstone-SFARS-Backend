using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Entities;
using System.Linq.Expressions;

namespace SFARS.Domain.Specifications.Reels
{
    public class ReelSpecification : BaseSpecification<Reel>
    {
        public ReelSpecification(Expression<Func<Reel, bool>> criteria) : base(criteria) { }

        public static ReelSpecification Feed(Guid? currentUserId, int skip, int take)
        {
            var spec = new ReelSpecification(r => !r.IsHidden);
            spec.AddOrderByDescending(r => r.CreatedAt);
            spec.ApplyPaging(take, skip);
            spec.ApplyInclude(q => q.Include(r => r.User));
            return spec;
        }

        public static ReelSpecification ByUserId(Guid targetUserId, int skip, int take)
        {
            var spec = new ReelSpecification(r => r.UserId == targetUserId && !r.IsHidden);
            spec.AddOrderByDescending(r => r.CreatedAt);
            spec.ApplyPaging(take, skip);
            spec.ApplyInclude(q => q.Include(r => r.User));
            return spec;
        }

        public static ReelSpecification ForCount(Guid? targetUserId = null)
        {
            if (targetUserId.HasValue)
            {
                return new ReelSpecification(r => r.UserId == targetUserId.Value && !r.IsHidden);
            }
            return new ReelSpecification(r => !r.IsHidden);
        }
    }
}
