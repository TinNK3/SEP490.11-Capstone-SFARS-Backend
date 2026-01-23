using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class Transaction : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? MissionId { get; set; }
    public decimal Amount { get; set; }
    public CurrencyType Currency { get; set; } = CurrencyType.VND;
    public TransactionType Type { get; set; } // Deposit, Payment
    public TransactionStatus Status { get; set; } // Pending, Success
    public PaymentGateway? PaymentGateway { get; set; }
    public string? GatewayTransactionId { get; set; }
    public string? Description { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual RescueMission? Mission { get; set; }
}