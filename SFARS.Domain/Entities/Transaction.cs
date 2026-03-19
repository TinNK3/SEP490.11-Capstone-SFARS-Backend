using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class Transaction : BaseEntity
{
    public string? TransactionCode { get; set; }
    public Guid UserId { get; set; }
    public Guid? MissionId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime? TransactionDate { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    
    // Store payment information
    public string? QrCode { get; set; }
    
    // Payment link ID
    public string? PaymentLinkId { get; set; }

    //Mapping Entities
    public virtual User User { get; set; } = null!;
    // public virtual RescueMission? Mission { get; set; }
}