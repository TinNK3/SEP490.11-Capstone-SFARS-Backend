using SFARS.Domain.Specifications;
using DomainTransaction = SFARS.Domain.Entities.Transaction;

namespace SFARS.Application.Specifications;

public class TransactionByUserSpec : BaseSpecification<DomainTransaction>
{
    public TransactionByUserSpec(Guid userId, int page, int pageSize)
        : base(t => t.UserId == userId)
    {
        AddOrderByDescending(t => t.CreatedAt);

        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : pageSize;
        ApplyPaging(safePageSize, (safePage - 1) * safePageSize);
    }
}
