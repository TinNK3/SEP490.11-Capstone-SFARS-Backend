using SFARS.Domain.Specifications;
using DomainTransaction = SFARS.Domain.Entities.Transaction;

namespace SFARS.Application.Specifications;

public class TransactionByUserSpec : BaseSpecification<DomainTransaction>
{
    public TransactionByUserSpec(Guid userId, int pageIndex, int pageSize)
        : base(t => t.UserId == userId)
    {
        AddOrderByDescending(t => t.CreatedAt);

        var safePageIndex = pageIndex < 1 ? 1 : pageIndex;
        var safePageSize = pageSize < 1 ? 20 : pageSize;
        ApplyPaging(safePageSize, (safePageIndex - 1) * safePageSize);
    }
}
