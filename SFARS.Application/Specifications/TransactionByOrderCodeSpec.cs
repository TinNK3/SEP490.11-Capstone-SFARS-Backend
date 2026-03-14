using SFARS.Domain.Specifications;
using DomainTransaction = SFARS.Domain.Entities.Transaction;

namespace SFARS.Application.Specifications;

public class TransactionByOrderCodeSpec : BaseSpecification<DomainTransaction>
{
    public TransactionByOrderCodeSpec(string orderCode)
        : base(t => t.TransactionCode == orderCode)
    {
    }
}
