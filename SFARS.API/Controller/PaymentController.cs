using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Net.payOS.Types;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Transaction;
using SFARS.Application.Dtos.Transaction;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Params;
using SFARS.Infrastructure.Configurations;

namespace SFARS.API.Controller;

/// <summary>
/// Payment endpoints — create a PayOS payment link, track status, cancel, and receive webhook.
/// </summary>
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly ITransactionService<TransactionDto> _transactionService;
    private readonly IWebHostEnvironment _env;
    private readonly PayOSSettings _payOsSettings;

    public PaymentController(
        ITransactionService<TransactionDto> transactionService,
        IWebHostEnvironment env,
        IOptions<PayOSSettings> payOsSettings)
    {
        _transactionService = transactionService;
        _env = env;
        _payOsSettings = payOsSettings.Value;
    }

    /// <summary>
    /// [Admin] Get paginated donation list across the whole system.
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpGet(APIRoute.Admin.GetAllPayments, Name = nameof(GetAllPaymentsAsync))]
    public async Task<IActionResult> GetAllPaymentsAsync(
        [FromQuery] TransactionSpecParams specParams,
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 20)
    {
        var result = await _transactionService.GetAllTransactionsAsync(specParams, pageIndex, pageSize);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Get donation overview statistics for dashboard.
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpGet(APIRoute.Admin.GetPaymentOverview, Name = nameof(GetPaymentOverviewAsync))]
    public async Task<IActionResult> GetPaymentOverviewAsync([FromQuery] TransactionSpecParams specParams)
    {
        var result = await _transactionService.GetTransactionOverviewAsync(specParams);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// Create a new pending payment and receive a PayOS VietQR / checkout URL.
    /// </summary>
    [Authorize]
    [HttpPost(APIRoute.Payment.Create, Name = nameof(CreatePaymentAsync))]
    public async Task<IActionResult> CreatePaymentAsync([FromBody] CreateTransactionRequest req)
    {
        var userId = User.GetUserId();
        var result = await _transactionService.CreateTransactionAsync(userId, req.ToTransactionDto());
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// Get payment details and current status.
    /// </summary>
    [Authorize]
    [HttpGet(APIRoute.Payment.GetById, Name = nameof(GetPaymentByIdAsync))]
    public async Task<IActionResult> GetPaymentByIdAsync([FromRoute] Guid id)
    {
        var result = await _transactionService.GetTransactionByIdAsync(id);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// Cancel a pending payment and invalidate the PayOS payment link.
    /// </summary>
    [Authorize]
    [HttpPatch(APIRoute.Payment.Cancel, Name = nameof(CancelPaymentAsync))]
    public async Task<IActionResult> CancelPaymentAsync([FromRoute] Guid id, [FromQuery] string? reason)
    {
        var userId = User.GetUserId();
        var isAdmin = User.IsInRole(UserTypeConstants.Admin);
        var result = await _transactionService.CancelTransactionAsync(id, userId, reason, isAdmin);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// Receive PayOS payment webhook. Verifies HMAC signature and updates transaction status.
    /// Returns HTTP 200 for both success and already-processed (idempotent).
    /// Signature bypass can only be enabled via internal config in Development.
    /// </summary>
    [AllowAnonymous]
    [HttpPost(APIRoute.Webhook.PayOs, Name = nameof(PayOsWebhookAsync))]
    public async Task<IActionResult> PayOsWebhookAsync([FromBody] WebhookType webhookBody)
    {
        // Security: bypass is controlled only by server-side config and only in Development.
        var allowSkipSignature = _env.IsDevelopment() && _payOsSettings.AllowSkipSignature;
        var result = await _transactionService.HandlePaymentWebhookAsync(webhookBody, allowSkipSignature);

        // Always return 200 to PayOS so it stops retrying
        return Ok(result);
    }
}