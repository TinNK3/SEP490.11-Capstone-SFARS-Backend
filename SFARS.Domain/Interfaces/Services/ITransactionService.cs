using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Models;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Interfaces.Services;

public interface ITransactionService<TDto> where TDto : class
{
    /// <summary>
    /// Get paged transaction history for current user.
    /// </summary>
    Task<IServiceResult> GetMyTransactionsAsync(Guid userId, TransactionSpecParams specParams);

    /// <summary>
    /// [Admin] Get paged transactions across the whole system.
    /// </summary>
    Task<IServiceResult> GetAllTransactionsAsync(TransactionSpecParams specParams);

    /// <summary>
    /// [Admin] Get donation overview statistics.
    /// </summary>
    Task<IServiceResult> GetTransactionOverviewAsync(TransactionSpecParams specParams);

    /// <summary>
    /// Create a new pending transaction and generate a PayOS payment link + QR code.
    /// </summary>
    Task<IServiceResult> CreateTransactionAsync(Guid userId, TDto dto);

    /// <summary>
    /// Get transaction details by ID.
    /// </summary>
    Task<IServiceResult> GetTransactionByIdAsync(Guid id);

    /// <summary>
    /// Process incoming payment webhook: verify signature, update transaction status.
    /// Idempotent — safe to call multiple times for the same webhook.
    /// </summary>
    /// <param name="webhookBody">Webhook payload from PayOS</param>
    /// <param name="skipSignature">For testing only: skip signature verification</param>
    Task<IServiceResult> HandlePaymentWebhookAsync(object webhookBody, bool skipSignature = false);

    /// <summary>
    /// Cancel a pending transaction (invalidates the PayOS payment link).
    /// </summary>
    Task<IServiceResult> CancelTransactionAsync(Guid id, Guid requestUserId, string? reason, bool isAdminRequest = false);
}
