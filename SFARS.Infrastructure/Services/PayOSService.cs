using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Net.payOS;
using Net.payOS.Types;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Models;
using SFARS.Infrastructure.Configurations;

namespace SFARS.Infrastructure.Services;

/// <summary>
/// PayOS payment gateway service implementation.
/// Handles webhook verification and payment operations.
/// </summary>
public class PayOSService : IPayOSService
{
    private readonly PayOS _payOsClient;
    private readonly ILogger<PayOSService> _logger;

    public PayOSService(
        IOptions<PayOSSettings> payOsSettings,
        ILogger<PayOSService> logger)
    {
        _logger = logger;
        
        var settings = payOsSettings.Value;
        
        // Validate required configuration
        if (string.IsNullOrWhiteSpace(settings.ClientId))
            throw new InvalidOperationException("PayOS ClientId is not configured.");
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException("PayOS ApiKey is not configured.");
        if (string.IsNullOrWhiteSpace(settings.ChecksumKey))
            throw new InvalidOperationException("PayOS ChecksumKey is not configured.");

        // Initialize PayOS SDK client
        _payOsClient = new PayOS(settings.ClientId, settings.ApiKey, settings.ChecksumKey);
        
        _logger.LogInformation("PayOS Service initialized successfully.");
    }

    /// <inheritdoc />
    public async Task<PaymentWebhookDto> VerifyAndParseWebhookAsync(object webhookBody, bool skipSignature = false)
    {
        try
        {
            // Cast to PayOS SDK webhook type
            if (webhookBody is not WebhookType webhookData)
            {
                _logger.LogWarning("Invalid webhook body type. Expected WebhookType.");
                throw new UnauthorizedAccessException("Invalid webhook data format.");
            }

            WebhookData verifiedData;

            if (skipSignature)
            {
                // TESTING ONLY: Skip signature verification and use raw data
                _logger.LogWarning("⚠️ TESTING MODE: Skipping PayOS webhook signature verification");
                verifiedData = webhookData.data;
            }
            else
            {
                // PRODUCTION: Verify HMAC signature using PayOS SDK
                verifiedData = _payOsClient.verifyPaymentWebhookData(webhookData);
            }
            
            _logger.LogInformation(
                "PayOS webhook verified successfully. OrderCode: {OrderCode}, Code: {Code}",
                verifiedData.orderCode,
                verifiedData.code);

            // Map to gateway-agnostic DTO
            return await Task.FromResult(new PaymentWebhookDto
            {
                OrderCode = verifiedData.orderCode,
                Code = verifiedData.code,
                Description = verifiedData.desc,
                Signature = webhookData.signature ?? string.Empty
            });
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            // Signature verification failed or SDK error
            _logger.LogError(ex, "PayOS webhook verification failed.");
            throw new UnauthorizedAccessException("Invalid webhook signature or verification failed.", ex);
        }
    }
}
