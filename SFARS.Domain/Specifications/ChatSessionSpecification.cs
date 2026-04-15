using SFARS.Domain.Entities;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Specifications;

public class ChatSessionSpecification : BaseSpecification<ChatSession>
{
    public ChatSessionSpecification(Guid userId, BaseSpecParams specParams, bool isCount = false)
        : base(x => 
            x.UserId == userId && 
            x.IsActive &&
            (string.IsNullOrEmpty(specParams.Search) || (x.Title != null && x.Title.ToLower().Contains(specParams.Search.ToLower()))))
    {
        if (!isCount)
        {
            AddOrderByDescending(x => x.LastMessageAt ?? x.CreatedAt);
            ApplyPaging(specParams.GetTake(), specParams.GetSkip());
        }
    }
}