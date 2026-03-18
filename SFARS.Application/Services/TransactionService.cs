using MapsterMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Net.payOS;
using Net.payOS.Types;
using SFARS.Application.Common;
using SFARS.Application.Configurations;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.Transaction;
using SFARS.Application.Specifications;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Models;
using SFARS.Domain.Specifications.Params;
using SFARS.Infrastructure.Configurations;
using SFARS.Infrastructure.Helpers;
using DomainTransaction = SFARS.Domain.Entities.Transaction;

namespace SFARS.Application.Services;

public class TransactionService : ITransactionService<TransactionDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ISystemMessageService _msgService;
    private readonly ILogger<TransactionService> _logger;
    private readonly IPayOSService _payOsService;
    private readonly PayOS _payOs;
    private readonly string _returnUrl;
    private readonly string _cancelUrl;
    private readonly int _expiredInMinutes;

    public TransactionService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ISystemMessageService msgService,
        ILogger<TransactionService> logger,
        IPayOSService payOsService,
        PayOS payOs,
        IOptions<PayOSSettings> payOsOptions,
        IOptions<PaymentSettings> paymentOptions)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _msgService = msgService;
        _logger = logger;
        _payOsService = payOsService;
        _payOs = payOs;

        var payOsSettings = payOsOptions.Value;
        var paymentSettings = paymentOptions.Value;

        _returnUrl = payOsSettings.ReturnUrl;
        _cancelUrl = payOsSettings.CancelUrl;
        _expiredInMinutes = paymentSettings.TransactionExpiredInMinutes > 0
            ? paymentSettings.TransactionExpiredInMinutes
            : PaymentConstants.DefaultExpirationMinutes;
    }

    public async Task<IServiceResult> GetMyTransactionsAsync(Guid userId, TransactionSpecParams specParams)
    {
        specParams ??= new TransactionSpecParams();
        var page = specParams.GetPage();
        var limit = specParams.GetTake();

        var countSpec = TransactionSpecification.CountForUser(userId, specParams);
        var totalItems = await _unitOfWork.Repository<DomainTransaction, Guid>().CountAsync(countSpec);

        if (totalItems == 0)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0004,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004),
                new PaginatedResultDto<TransactionDto>(Enumerable.Empty<TransactionDto>(), page, limit, 0, 0));
        }

        var listSpec = TransactionSpecification.ListForUser(userId, specParams);
        var transactions = await _unitOfWork.Repository<DomainTransaction, Guid>().GetAllWithSpecAsync(listSpec, tracked: false);

        var data = transactions.Select(_mapper.Map<TransactionDto>).ToList();
        var totalPages = (int)Math.Ceiling((double)totalItems / limit);

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            new PaginatedResultDto<TransactionDto>(data, page, limit, totalPages, totalItems));
    }

    public async Task<IServiceResult> GetAllTransactionsAsync(TransactionSpecParams specParams)
    {
        specParams ??= new TransactionSpecParams();
        var page = specParams.GetPage();
        var limit = specParams.GetTake();

        var countSpec = TransactionSpecification.CountForAdmin(specParams);
        var totalItems = await _unitOfWork.Repository<DomainTransaction, Guid>().CountAsync(countSpec);

        if (totalItems == 0)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0004,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004),
                new PaginatedResultDto<TransactionDto>(Enumerable.Empty<TransactionDto>(), page, limit, 0, 0));
        }

        var listSpec = TransactionSpecification.ListForAdmin(specParams);
        var transactions = await _unitOfWork.Repository<DomainTransaction, Guid>().GetAllWithSpecAsync(listSpec, tracked: false);

        var data = transactions.Select(_mapper.Map<TransactionDto>).ToList();
        var totalPages = (int)Math.Ceiling((double)totalItems / limit);

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            new PaginatedResultDto<TransactionDto>(data, page, limit, totalPages, totalItems));
    }

    public async Task<IServiceResult> GetTransactionOverviewAsync(TransactionSpecParams specParams)
    {
        specParams ??= new TransactionSpecParams();

        // Use specification to build the base query
        var allSpec = TransactionSpecification.ListForAdminNoPaging(specParams);
        var repository = _unitOfWork.Repository<DomainTransaction, Guid>();
        
        // Execute aggregation queries directly on database to avoid loading all data into memory
        var baseQuery = await repository.GetAllWithSpecAsync(allSpec, tracked: false);
        var queryable = baseQuery.AsQueryable();

        // Perform calculations on database side
        var totalCount = queryable.Count();
        
        if (totalCount == 0)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                new TransactionOverviewDto
                {
                    TotalTransactions = 0,
                    TotalAttemptedAmount = 0,
                    TotalPaidAmount = 0,
                    PendingCount = 0,
                    PaidCount = 0,
                    CancelledCount = 0,
                    ExpiredCount = 0,
                    PaidRatePercent = 0
                });
        }

        var totalAttemptedAmount = queryable.Sum(t => t.Amount);
        var totalPaidAmount = queryable.Where(t => t.Status == PaymentStatus.Paid).Sum(t => t.Amount);
        var pendingCount = queryable.Count(t => t.Status == PaymentStatus.Pending);
        var paidCount = queryable.Count(t => t.Status == PaymentStatus.Paid);
        var cancelledCount = queryable.Count(t => t.Status == PaymentStatus.Cancelled);
        var expiredCount = queryable.Count(t => t.Status == PaymentStatus.Expired);

        var overview = new TransactionOverviewDto
        {
            TotalTransactions = totalCount,
            TotalAttemptedAmount = totalAttemptedAmount,
            TotalPaidAmount = totalPaidAmount,
            PendingCount = pendingCount,
            PaidCount = paidCount,
            CancelledCount = cancelledCount,
            ExpiredCount = expiredCount,
            PaidRatePercent = totalCount == 0 ? 0 : Math.Round((decimal)paidCount * 100 / totalCount, 2)
        };

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            overview);
    }

    public async Task<IServiceResult> CreateTransactionAsync(Guid userId, TransactionDto dto)
    {
        try
        {
            // Validate using FluentValidation
            var validationResult = await Validations.ValidatorExtensions.ValidateAsync(dto);
            if (validationResult != null)
            {
                var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0001,
                    errors);
            }

            var amount = dto.Amount;
            var description = dto.Description;

            // Validate amount meets PayOS requirements (no auto-rounding for financial integrity)
            if (!PayOSHelper.ValidateAmount(amount, out var amountError))
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0001,
                    amountError!);
            }

            // Generate a unique numeric order code (PayOS requires long)
            var orderCode = PayOSHelper.GenerateOrderCode();
            var expiredAt = DateTime.UtcNow.AddMinutes(_expiredInMinutes);
            var payOsDescription = PayOSHelper.TruncateDescription(description ?? string.Empty);

            // Create PayOS payment link
            var items = new List<ItemData> { new(PaymentConstants.PaymentItemDescription, 1, (int)amount) };
            var paymentData = new PaymentData(
                orderCode: orderCode,
                amount: (int)amount,
                description: payOsDescription,
                items: items,
                cancelUrl: _cancelUrl,
                returnUrl: _returnUrl,
                expiredAt: (int)((DateTimeOffset)expiredAt.ToUniversalTime()).ToUnixTimeSeconds());

            CreatePaymentResult paymentResult;
            try
            {
                paymentResult = await _payOs.createPaymentLink(paymentData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayOS createPaymentLink failed for orderCode {OrderCode}", orderCode);
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    "Không thể tạo liên kết thanh toán. Vui lòng thử lại.");
            }

            // Persist transaction as Pending
            var transaction = new DomainTransaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = amount,
                Description = description,
                Status = PaymentStatus.Pending,
                TransactionCode = orderCode.ToString(),
                PaymentLinkId = paymentResult.paymentLinkId,
                QrCode = paymentResult.qrCode,
                CreatedAt = DateTime.UtcNow,
                ExpiredAt = expiredAt
            };

            await _unitOfWork.Repository<DomainTransaction, Guid>().AddAsync(transaction);
            await _unitOfWork.SaveChangesAsync();

            var resultDto = _mapper.Map<TransactionDto>(transaction);
            resultDto.CheckoutUrl = paymentResult.checkoutUrl;

            return new ServiceResult(
                ResultCodeConst.SYS_Success0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001),
                resultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateTransactionAsync failed for user {UserId}", userId);
            throw;
        }
    }

    public async Task<IServiceResult> GetTransactionByIdAsync(Guid id)
    {
        var transaction = await _unitOfWork.Repository<DomainTransaction, Guid>().GetByIdAsync(id);

        if (transaction is null)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0004,
                "Không tìm thấy giao dịch");
        }

        return new ServiceResult(
            ResultCodeConst.SYS_Success0001,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001),
            _mapper.Map<TransactionDto>(transaction));
    }


    public async Task<IServiceResult> HandlePaymentWebhookAsync(object webhookBody, bool skipSignature = false)
    {
        if (webhookBody == null)
        {
            _logger.LogError("Webhook payload is null");
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, "Invalid webhook payload");
        }

        PaymentWebhookDto webhook;
        try
        {
            // Verify signature and parse webhook data via PayOS service
            webhook = await _payOsService.VerifyAndParseWebhookAsync(webhookBody, skipSignature);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Invalid webhook signature");
            return new ServiceResult(
                ResultCodeConst.Payment_Warning0001,
                await _msgService.GetMessageAsync(ResultCodeConst.Payment_Warning0001));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify webhook");
            return new ServiceResult(
                ResultCodeConst.SYS_Fail0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }

        _logger.LogInformation(
            "Payment webhook received: orderCode={OrderCode}, code={Code}, desc={Desc}",
            webhook.OrderCode, webhook.Code, webhook.Description);

        // Use database transaction to prevent race condition when concurrent webhooks arrive
        try
        {
            var orderCodeStr = webhook.OrderCode.ToString();
            
            // Find and lock transaction record (prevents concurrent updates)
            var transaction = await _unitOfWork.Repository<DomainTransaction, Guid>()
                .GetWithSpecAsync(new TransactionByOrderCodeSpec(orderCodeStr), tracked: true);

            if (transaction is null)
            {
                _logger.LogWarning("Payment webhook: no transaction found for orderCode {OrderCode}", webhook.OrderCode);
                return new ServiceResult(
                    ResultCodeConst.Payment_Warning0002,
                    await _msgService.GetMessageAsync(ResultCodeConst.Payment_Warning0002));
            }

            // Idempotency guard — already processed (prevents duplicate processing)
            if (transaction.Status != PaymentStatus.Pending)
            {
                _logger.LogInformation(
                    "Webhook already processed for orderCode {OrderCode}, current status: {Status}",
                    webhook.OrderCode, transaction.Status);
                    
                return new ServiceResult(
                    ResultCodeConst.SYS_Success0001,
                    "Webhook already processed (idempotent)",
                    _mapper.Map<TransactionDto>(transaction));
            }

            // Determine new status from payment gateway response code
            if (webhook.Code == "00")
            {
                transaction.Status = PaymentStatus.Paid;
                transaction.TransactionDate = DateTime.UtcNow;
                _logger.LogInformation("Transaction {OrderCode} marked as Paid", webhook.OrderCode);
            }
            else
            {
                transaction.Status = PaymentStatus.Cancelled;
                transaction.CancelledAt = DateTime.UtcNow;
                transaction.CancellationReason = $"Payment gateway code: {webhook.Code} - {webhook.Description}";
                _logger.LogInformation("Transaction {OrderCode} marked as Cancelled, reason: {Code}", 
                    webhook.OrderCode, webhook.Code);
            }

            transaction.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<DomainTransaction, Guid>().Update(transaction);
            
            // SaveChangesWithTransactionAsync ensures atomicity and prevents race conditions
            await _unitOfWork.SaveChangesWithTransactionAsync();

            return new ServiceResult(
                ResultCodeConst.SYS_Success0001,
                "Webhook processed successfully",
                _mapper.Map<TransactionDto>(transaction));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process webhook for orderCode {OrderCode}", webhook.OrderCode);
            return new ServiceResult(
                ResultCodeConst.SYS_Fail0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }
    }

    public async Task<IServiceResult> CancelTransactionAsync(Guid id, Guid requestUserId, string? reason, bool isAdminRequest = false)
    {
        var transaction = await _unitOfWork.Repository<DomainTransaction, Guid>().GetByIdAsync(id);

        if (transaction is null)
        {
            return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Không tìm thấy giao dịch");
        }

        // Admin can cancel any transaction; regular users can only cancel their own
        if (!isAdminRequest && transaction.UserId != requestUserId)
        {
            return new ServiceResult(ResultCodeConst.SYS_Warning0007, "Bạn không có quyền hủy giao dịch này");
        }

        if (transaction.Status != PaymentStatus.Pending)
        {
            return new ServiceResult(ResultCodeConst.SYS_Warning0001,
                $"Không thể hủy giao dịch ở trạng thái '{transaction.Status}'");
        }

        // Cancel PayOS payment link
        if (!string.IsNullOrEmpty(transaction.TransactionCode) &&
            long.TryParse(transaction.TransactionCode, out var orderCode))
        {
            try
            {
                await _payOs.cancelPaymentLink(orderCode, reason ?? "Cancelled by user");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cancel PayOS payment link for orderCode {OrderCode}", orderCode);
                // Continue to cancel locally even if PayOS call fails
            }
        }

        transaction.Status = PaymentStatus.Cancelled;
        transaction.CancelledAt = DateTime.UtcNow;
        transaction.CancellationReason = reason;
        transaction.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Repository<DomainTransaction, Guid>().Update(transaction);
        await _unitOfWork.SaveChangesAsync();

        return new ServiceResult(
            ResultCodeConst.SYS_Success0001,
            "Đã hủy giao dịch thành công",
            _mapper.Map<TransactionDto>(transaction));
    }
}
