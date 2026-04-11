using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Entities;
using System.Linq.Expressions;

namespace SFARS.Domain.Specifications.Reels
{
    public class ReelSpecification : BaseSpecification<Reel>
    {
        public ReelSpecification(Expression<Func<Reel, bool>> criteria) : base(criteria) { }

        public ReelSpecification(SFARS.Domain.Specifications.Params.ReelSpecParams specParams, Guid? targetUserId = null)
            : base(r => (!targetUserId.HasValue || r.UserId == targetUserId) &&
                        !r.IsHidden && !r.IsHiddenByAdmin &&
                        (string.IsNullOrEmpty(specParams.Search) || (r.Caption != null && r.Caption.ToLower().Contains(specParams.Search.ToLower()))) &&
                        (!specParams.CreatedFrom.HasValue || r.CreatedAt >= specParams.CreatedFrom) &&
                        (!specParams.CreatedTo.HasValue || r.CreatedAt <= specParams.CreatedTo))
        {
            ApplyPaging(specParams.GetTake(), specParams.GetSkip());
            ApplyInclude(q => q.Include(r => r.User));

            if (!string.IsNullOrEmpty(specParams.Sort))
            {
                switch (specParams.Sort.ToLower())
                {
                    case "likecount":
                        AddOrderByDescending(r => r.LikeCount);
                        break;
                    case "commentcount":
                        AddOrderByDescending(r => r.CommentCount);
                        break;
                    default:
                        AddOrderByDescending(r => r.CreatedAt);
                        break;
                }
            }
            else
            {
                AddOrderByDescending(r => r.CreatedAt);
            }
        }

        public static ReelSpecification Feed(Guid? currentUserId, int skip, int take)
        {
            var spec = new ReelSpecification(r => !r.IsHidden && !r.IsHiddenByAdmin);
            spec.AddOrderByDescending(r => r.CreatedAt);
            spec.ApplyPaging(take, skip);
            spec.ApplyInclude(q => q.Include(r => r.User));
            return spec;
        }

        public static ReelSpecification ByUserId(Guid targetUserId, int skip, int take)
        {
            var spec = new ReelSpecification(r => r.UserId == targetUserId && !r.IsHidden && !r.IsHiddenByAdmin);
            spec.AddOrderByDescending(r => r.CreatedAt);
            spec.ApplyPaging(take, skip);
            spec.ApplyInclude(q => q.Include(r => r.User));
            return spec;
        }

        public static ReelSpecification ForCount(SFARS.Domain.Specifications.Params.ReelSpecParams specParams, Guid? targetUserId = null)
        {
            return new ReelSpecification(r => (!targetUserId.HasValue || r.UserId == targetUserId) &&
                                              !r.IsHidden && !r.IsHiddenByAdmin &&
                                              (string.IsNullOrEmpty(specParams.Search) || r.Caption.ToLower().Contains(specParams.Search.ToLower())) &&
                                              (!specParams.CreatedFrom.HasValue || r.CreatedAt >= specParams.CreatedFrom) &&
                                              (!specParams.CreatedTo.HasValue || r.CreatedAt <= specParams.CreatedTo));
        }

        public static ReelSpecification ForCount(Guid? targetUserId = null)
        {
            if (targetUserId.HasValue)
            {
                return new ReelSpecification(r => r.UserId == targetUserId.Value && !r.IsHidden && !r.IsHiddenByAdmin);
            }
            return new ReelSpecification(r => !r.IsHidden && !r.IsHiddenByAdmin);
        }
    }
}
