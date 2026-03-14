using SFARS.Domain.Entities;

namespace SFARS.Domain.Specifications;

/// <summary>
/// Specification for querying Transaction by PayOS order code.
/// </summary>
public class TransactionByOrderCodeSpec : BaseSpecification<Transaction>
{
    /// <summary>
    /// Find transaction by TransactionCode (which stores PayOS orderCode).
    /// </summary>
    /// <param name="orderCode">PayOS order code (long converted to string)</param>
    public TransactionByOrderCodeSpec(string orderCode) 
        : base(t => t.TransactionCode == orderCode)
    {
    }
}
