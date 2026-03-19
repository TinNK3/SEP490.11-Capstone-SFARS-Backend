using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Specifications.Params;

public class TransactionSpecParams : BaseSpecParams
{
    // Filter by payment status
    public PaymentStatus? Status { get; set; }

    // Filter by date range and amount range
    public DateTime?[]? CreateDateRange { get; set; }
}
