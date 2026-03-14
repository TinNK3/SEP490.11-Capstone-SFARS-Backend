namespace SFARS.Domain.Models;

/// <summary>
/// Data Transfer Object for payment gateway webhook notifications.
/// Decouples Domain/Application layer from external payment SDK types.
/// </summary>
public class PaymentWebhookDto
{
    /// <summary>
    /// Payment order code (unique identifier from payment gateway).
    /// </summary>
    public long OrderCode { get; set; }

    /// <summary>
    /// Payment result code (e.g., "00" = success).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Payment result description from gateway.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Webhook HMAC signature for verification.
    /// </summary>
    public string Signature { get; set; } = string.Empty;
}
