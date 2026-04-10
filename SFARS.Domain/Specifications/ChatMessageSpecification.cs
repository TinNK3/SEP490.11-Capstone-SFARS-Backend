using SFARS.Domain.Entities;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Specifications;

public class ChatMessageSpecification : BaseSpecification<ChatMessage>
{
    public ChatMessageSpecification(Guid sessionId, BaseSpecParams specParams, bool isCount = false)
        : base(x => 
            x.ChatSessionId == sessionId &&
            (string.IsNullOrEmpty(specParams.Search) || (x.Content != null && x.Content.ToLower().Contains(specParams.Search.ToLower()))))
    {
        if (!isCount)
        {
            AddOrderBy(x => x.CreatedAt);
            ApplyPaging(specParams.GetTake(), specParams.GetSkip());
        }
    }
}