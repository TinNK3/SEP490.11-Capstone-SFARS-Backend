using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Transaction;

public class TransactionDto
{
    public Guid Id { get; set; }
    public string? TransactionCode { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public PaymentStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();
    public DateTime? TransactionDate { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Payment info
    public string? QrCode { get; set; }
    public string? PaymentLinkId { get; set; }
    public string? CheckoutUrl { get; set; }
}
