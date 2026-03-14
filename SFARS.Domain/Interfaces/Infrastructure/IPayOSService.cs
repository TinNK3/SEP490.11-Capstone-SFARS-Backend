using SFARS.Domain.Models;

namespace SFARS.Domain.Interfaces.Infrastructure;

/// <summary>
/// Abstracts PayOS payment gateway operations.
/// Handles webhook verification and payment link generation.
/// </summary>
public interface IPayOSService
{
    /// <summary>
    /// Verifies PayOS webhook HMAC signature and extracts payment data.
    /// </summary>
    /// <param name="webhookBody">Raw webhook data from PayOS</param>
    /// <param name="skipSignature">For testing: skip signature verification (default: false)</param>
    /// <returns>Validated and parsed webhook DTO</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when signature verification fails</exception>
    Task<PaymentWebhookDto> VerifyAndParseWebhookAsync(object webhookBody, bool skipSignature = false);
}
