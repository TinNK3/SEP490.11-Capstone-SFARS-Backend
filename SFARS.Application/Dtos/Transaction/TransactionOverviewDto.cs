namespace SFARS.Application.Dtos.Transaction;

public class TransactionOverviewDto
{
    public int TotalTransactions { get; set; }
    public decimal TotalAttemptedAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }

    public int PendingCount { get; set; }
    public int PaidCount { get; set; }
    public int CancelledCount { get; set; }
    public int ExpiredCount { get; set; }

    public decimal PaidRatePercent { get; set; }
}
