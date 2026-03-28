using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Specifications.Params;
using System.Linq.Expressions;

namespace SFARS.Domain.Specifications.Community
{
    public class CommunityPostSpecification : BaseSpecification<ContentPost>
    {
        private CommunityPostSpecification(Expression<Func<ContentPost, bool>> criteria)
            : base(criteria)
        {
        }

        public static CommunityPostSpecification List(CommunityPostSpecParams p, Guid currentUserId)
        {
            var spec = new CommunityPostSpecification(post => post.Type == PostType.Community && (post.IsPublished || post.AuthorId == currentUserId));

            ApplySearchFilter(spec, p.Search);
            ApplySort(spec, p.Sort);
            spec.ApplyPaging(p.GetTake(), p.GetSkip());

            spec.ApplyInclude(q => q
                .Include(post => post.Author)
                .Include(post => post.Medias)
                .Include(post => post.Likes.Where(l => l.UserId == currentUserId)));
            spec.EnableSplitQuery();

            return spec;
        }

        public static CommunityPostSpecification Count(CommunityPostSpecParams p, Guid currentUserId)
        {
            var spec = new CommunityPostSpecification(post => post.Type == PostType.Community && (post.IsPublished || post.AuthorId == currentUserId));
            ApplySearchFilter(spec, p.Search);
            return spec;
        }

        private static void ApplySearchFilter(CommunityPostSpecification spec, string? rawSearch)
        {
            var keyword = rawSearch?.Trim();
            if (string.IsNullOrWhiteSpace(keyword))
                return;

            spec.AddFilter(post => post.BodyContent!.Contains(keyword));
        }

        private static void ApplySort(CommunityPostSpecification spec, string? rawSort)
        {
            var sort = rawSort?.Trim();
            if (string.IsNullOrWhiteSpace(sort))
            {
                spec.AddOrderByDescending(post => post.CreatedAt);
                return;
            }

            var isDescending = sort.StartsWith("-");
            var key = isDescending ? sort[1..] : sort;

            switch (key.ToLowerInvariant())
            {
                case "createdat":
                    if (isDescending) spec.AddOrderByDescending(post => post.CreatedAt);
                    else spec.AddOrderBy(post => post.CreatedAt);
                    break;
                case "likecount":
                    if (isDescending) spec.AddOrderByDescending(post => post.LikeCount);
                    else spec.AddOrderBy(post => post.LikeCount);
                    break;
                case "commentcount":
                    if (isDescending) spec.AddOrderByDescending(post => post.CommentCount);
                    else spec.AddOrderBy(post => post.CommentCount);
                    break;
                default:
                    spec.AddOrderByDescending(post => post.CreatedAt);
                    break;
            }
        }
    }
}
