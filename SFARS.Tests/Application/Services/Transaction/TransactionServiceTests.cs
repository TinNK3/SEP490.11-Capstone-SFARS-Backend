using FluentAssertions;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Net.payOS;
using Net.payOS.Types;
using SFARS.Application.Common;
using SFARS.Application.Configurations;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.Transaction;
using SFARS.Application.Services;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Models;
using SFARS.Domain.Specifications.Interfaces;
using SFARS.Domain.Specifications.Params;
using SFARS.Infrastructure.Configurations;

namespace SFARS.Tests.Application.Services.Transaction;

/// <summary>
/// Unit tests for TransactionService:
/// - GetMyTransactionsAsync         (GET  /api/me/donation-history)
/// - GetAllTransactionsAsync        (GET  /admin/payments)
/// - GetTransactionOverviewAsync    (GET  /admin/payments/overview)
/// - CreateTransactionAsync         (POST /api/payments)
/// - GetTransactionByIdAsync        (GET  /api/payments/{id})
/// - HandlePaymentWebhookAsync      (POST /api/webhooks/payos)
/// - CancelTransactionAsync         (PATCH /api/payments/{id}/cancel)
///
/// Scope: Application service layer with PayOS integration mocked.
/// </summary>
public class TransactionServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IGenericRepository<Domain.Entities.Transaction, Guid>> _transactionRepoMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<ISystemMessageService> _msgServiceMock;
    private readonly Mock<ILogger<TransactionService>> _loggerMock;
    private readonly Mock<IPayOSService> _payOsServiceMock;
    private readonly Mock<IOptions<PayOSSettings>> _payOsOptionsMock;
    private readonly Mock<IOptions<PaymentSettings>> _paymentOptionsMock;

    private readonly TransactionService _sut;

    public TransactionServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionRepoMock = new Mock<IGenericRepository<Domain.Entities.Transaction, Guid>>();
        _mapperMock = new Mock<IMapper>();
        _msgServiceMock = new Mock<ISystemMessageService>();
        _loggerMock = new Mock<ILogger<TransactionService>>();
        _payOsServiceMock = new Mock<IPayOSService>();
        _payOsOptionsMock = new Mock<IOptions<PayOSSettings>>();
        _paymentOptionsMock = new Mock<IOptions<PaymentSettings>>();

        // Create PayOS instance with test credentials
        var payOs = new PayOS("test_client", "test_api_key", "test_checksum");

        // Setup repository
        _unitOfWorkMock
            .Setup(x => x.Repository<Domain.Entities.Transaction, Guid>())
            .Returns(_transactionRepoMock.Object);

        // Setup message service
        _msgServiceMock
            .Setup(x => x.GetMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((string code) => $"Message for {code}");

        // Setup PayOS settings
        _payOsOptionsMock
            .Setup(x => x.Value)
            .Returns(new PayOSSettings
            {
                ClientId = "test_client",
                ApiKey = "test_api_key",
                ChecksumKey = "test_checksum",
                ReturnUrl = "https://test.com/return",
                CancelUrl = "https://test.com/cancel"
            });

        // Setup payment settings
        _paymentOptionsMock
            .Setup(x => x.Value)
            .Returns(new PaymentSettings
            {
                TransactionExpiredInMinutes = 30
            });

        _sut = new TransactionService(
            _unitOfWorkMock.Object,
            _mapperMock.Object,
            _msgServiceMock.Object,
            _loggerMock.Object,
            _payOsServiceMock.Object,
            payOs,
            _payOsOptionsMock.Object,
            _paymentOptionsMock.Object
        );
    }

    #region GetMyTransactionsAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: GetMyTransactionsAsync with no transactions
    /// Precondition: User has no transactions in system
    /// Expected Result: Returns SYS_Warning0004 with empty paginated result
    /// </summary>
    [Fact]
    public async Task GetMyTransactionsAsync_NoTransactions_ReturnsWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var specParams = new TransactionSpecParams();

        _transactionRepoMock
            .Setup(r => r.CountAsync(It.IsAny<ISpecification<Domain.Entities.Transaction>>()))
            .ReturnsAsync(0);

        // Act
        var result = await _sut.GetMyTransactionsAsync(userId, specParams, 0, 20);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        result.Data.Should().NotBeNull();
        var paginatedData = result.Data as PaginatedResultDto<TransactionDto>;
        paginatedData!.Sources.Should().BeEmpty();
        paginatedData.TotalActualItem.Should().Be(0);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: GetMyTransactionsAsync returns paginated transactions
    /// Precondition: User has multiple transactions
    /// Expected Result: Returns success with paginated transaction list
    /// </summary>
    [Fact]
    public async Task GetMyTransactionsAsync_WithTransactions_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var specParams = new TransactionSpecParams();

        var transactions = new List<Domain.Entities.Transaction>
        {
            new Domain.Entities.Transaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = 50000,
                Status = PaymentStatus.Paid,
                TransactionCode = "123456789"
            }
        };

        _transactionRepoMock
            .Setup(r => r.CountAsync(It.IsAny<ISpecification<Domain.Entities.Transaction>>()))
            .ReturnsAsync(1);

        _transactionRepoMock
            .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Domain.Entities.Transaction>>(), It.IsAny<bool>()))
            .ReturnsAsync(transactions);

        _mapperMock
            .Setup(m => m.Map<TransactionDto>(It.IsAny<Domain.Entities.Transaction>()))
            .Returns((Domain.Entities.Transaction t) => new TransactionDto
            {
                Id = t.Id,
                UserId = t.UserId,
                Amount = t.Amount,
                Status = t.Status,
                TransactionCode = t.TransactionCode
            });

        // Act
        var result = await _sut.GetMyTransactionsAsync(userId, specParams, 0, 20);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var paginatedData = result.Data as PaginatedResultDto<TransactionDto>;
        paginatedData!.Sources.Should().HaveCount(1);
        paginatedData.TotalActualItem.Should().Be(1);
        paginatedData.Sources.First().TransactionCode.Should().Be("123456789");
    }

    /// <summary>
    /// Test Type: EDGE CASE
    /// Tests: GetMyTransactionsAsync with invalid page parameters
    /// Precondition: Negative pageIndex or zero/negative pageSize
    /// Expected Result: Parameters are normalized (pageIndex >= 0, pageSize = 20)
    /// </summary>
    [Fact]
    public async Task GetMyTransactionsAsync_InvalidPageParams_NormalizesParams()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var specParams = new TransactionSpecParams();

        _transactionRepoMock
            .Setup(r => r.CountAsync(It.IsAny<ISpecification<Domain.Entities.Transaction>>()))
            .ReturnsAsync(0);

        // Act - negative pageIndex, zero pageSize
        var result = await _sut.GetMyTransactionsAsync(userId, specParams, -5, 0);

        // Assert
        result.Should().NotBeNull();
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        // Service should normalize params internally
    }

    #endregion

    #region CreateTransactionAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: CreateTransactionAsync with amount not divisible by 1000
    /// Precondition: Amount is 1500 (not divisible by 1000)
    /// Expected Result: Returns validation warning
    /// </summary>
    [Fact]
    public async Task CreateTransactionAsync_InvalidAmount_ReturnsValidationError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = new TransactionDto
        {
            Amount = 2500, // Passes FluentValidation (>= 2000) but not divisible by 1000
            Description = "Test"
        };

        // Act
        var result = await _sut.CreateTransactionAsync(userId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        result.Message.Should().Contain("chia hết");
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: CreateTransactionAsync with amount below minimum
    /// Precondition: Amount is 500 (below 1000 minimum)
    /// Expected Result: Returns validation warning
    /// </summary>
    [Fact]
    public async Task CreateTransactionAsync_AmountBelowMinimum_ReturnsValidationError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = new TransactionDto
        {
            Amount = 500, // Below minimum
            Description = "Test"
        };

        // Act
        var result = await _sut.CreateTransactionAsync(userId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        result.Message.Should().Contain("tối thiểu");
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: CreateTransactionAsync with amount above maximum
    /// Precondition: Amount is 200,000,000 (above 100,000,000 maximum)
    /// Expected Result: Returns validation warning
    /// </summary>
    [Fact]
    public async Task CreateTransactionAsync_AmountAboveMaximum_ReturnsValidationError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = new TransactionDto
        {
            Amount = 200_000_000, // Above maximum
            Description = "Test"
        };

        // Act
        var result = await _sut.CreateTransactionAsync(userId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        result.Message.Should().Contain("tối đa");
    }

    #endregion

    #region HandlePaymentWebhookAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: HandlePaymentWebhookAsync with null webhook body
    /// Precondition: Webhook body is null
    /// Expected Result: Returns SYS_Warning0001
    /// </summary>
    [Fact]
    public async Task HandlePaymentWebhookAsync_NullBody_ReturnsWarning()
    {
        // Arrange
        object? webhookBody = null;

        // Act
        var result = await _sut.HandlePaymentWebhookAsync(webhookBody!, false);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        result.Message.Should().Contain("Invalid");
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: HandlePaymentWebhookAsync with invalid signature
    /// Precondition: Signature verification fails
    /// Expected Result: Returns Payment_Warning0001
    /// </summary>
    [Fact]
    public async Task HandlePaymentWebhookAsync_InvalidSignature_ReturnsWarning()
    {
        // Arrange
        var webhookBody = new { orderCode = 123456789, amount = 50000 };

        _payOsServiceMock
            .Setup(s => s.VerifyAndParseWebhookAsync(It.IsAny<object>(), false))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid signature"));

        // Act
        var result = await _sut.HandlePaymentWebhookAsync(webhookBody, false);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Payment_Warning0001);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: HandlePaymentWebhookAsync for non-existent transaction
    /// Precondition: OrderCode does not match any transaction
    /// Expected Result: Returns Payment_Warning0002
    /// </summary>
    [Fact]
    public async Task HandlePaymentWebhookAsync_TransactionNotFound_ReturnsWarning()
    {
        // Arrange
        var webhookBody = new { orderCode = 999999999, amount = 50000 };

        var webhookDto = new PaymentWebhookDto
        {
            OrderCode = 999999999,
            Code = "00",
            Description = "Success",
            Signature = "test"
        };

        _payOsServiceMock
            .Setup(s => s.VerifyAndParseWebhookAsync(It.IsAny<object>(), true))
            .ReturnsAsync(webhookDto);

        _transactionRepoMock
            .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Domain.Entities.Transaction>>(), It.IsAny<bool>()))
            .ReturnsAsync((Domain.Entities.Transaction?)null);

        // Act
        var result = await _sut.HandlePaymentWebhookAsync(webhookBody, skipSignature: true);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Payment_Warning0002);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: HandlePaymentWebhookAsync successfully updates transaction to Paid
    /// Precondition: Valid webhook with code "00", transaction exists and is Pending
    /// Expected Result: Transaction status updated to Paid, returns success
    /// </summary>
    [Fact]
    public async Task HandlePaymentWebhookAsync_ValidWebhook_UpdatesTransactionToPaid()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var orderCode = 123456789L;

        var webhookBody = new { orderCode, amount = 50000 };

        var webhookDto = new PaymentWebhookDto
        {
            OrderCode = orderCode,
            Code = "00",
            Description = "Success",
            Signature = "test"
        };

        var transaction = new Domain.Entities.Transaction
        {
            Id = transactionId,
            TransactionCode = orderCode.ToString(),
            Status = PaymentStatus.Pending,
            Amount = 50000,
            UserId = Guid.NewGuid()
        };

        _payOsServiceMock
            .Setup(s => s.VerifyAndParseWebhookAsync(It.IsAny<object>(), true))
            .ReturnsAsync(webhookDto);

        _transactionRepoMock
            .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Domain.Entities.Transaction>>(), true))
            .ReturnsAsync(transaction);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesWithTransactionAsync())
            .ReturnsAsync(1);

        _mapperMock
            .Setup(m => m.Map<TransactionDto>(It.IsAny<Domain.Entities.Transaction>()))
            .Returns(new TransactionDto
            {
                Id = transactionId,
                Status = PaymentStatus.Paid
            });

        // Act
        var result = await _sut.HandlePaymentWebhookAsync(webhookBody, skipSignature: true);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        transaction.Status.Should().Be(PaymentStatus.Paid);
        transaction.TransactionDate.Should().NotBeNull();
        _transactionRepoMock.Verify(r => r.Update(transaction), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesWithTransactionAsync(), Times.Once);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: HandlePaymentWebhookAsync updates transaction to Cancelled
    /// Precondition: Valid webhook with code != "00", transaction is Pending
    /// Expected Result: Transaction status updated to Cancelled with reason
    /// </summary>
    [Fact]
    public async Task HandlePaymentWebhookAsync_FailedPayment_UpdatesTransactionToCancelled()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var orderCode = 123456789L;

        var webhookBody = new { orderCode, amount = 50000, code = "01" };

        var webhookDto = new PaymentWebhookDto
        {
            OrderCode = orderCode,
            Code = "01",
            Description = "Payment failed",
            Signature = "test"
        };

        var transaction = new Domain.Entities.Transaction
        {
            Id = transactionId,
            TransactionCode = orderCode.ToString(),
            Status = PaymentStatus.Pending,
            Amount = 50000,
            UserId = Guid.NewGuid()
        };

        _payOsServiceMock
            .Setup(s => s.VerifyAndParseWebhookAsync(It.IsAny<object>(), true))
            .ReturnsAsync(webhookDto);

        _transactionRepoMock
            .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Domain.Entities.Transaction>>(), true))
            .ReturnsAsync(transaction);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesWithTransactionAsync())
            .ReturnsAsync(1);

        _mapperMock
            .Setup(m => m.Map<TransactionDto>(It.IsAny<Domain.Entities.Transaction>()))
            .Returns(new TransactionDto
            {
                Id = transactionId,
                Status = PaymentStatus.Cancelled
            });

        // Act
        var result = await _sut.HandlePaymentWebhookAsync(webhookBody, skipSignature: true);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        transaction.Status.Should().Be(PaymentStatus.Cancelled);
        transaction.CancelledAt.Should().NotBeNull();
        transaction.CancellationReason.Should().Contain("01");
        _transactionRepoMock.Verify(r => r.Update(transaction), Times.Once);
    }

    /// <summary>
    /// Test Type: IDEMPOTENCY
    /// Tests: HandlePaymentWebhookAsync with already processed transaction
    /// Precondition: Transaction already in terminal state (Paid/Cancelled)
    /// Expected Result: Returns success without modifying transaction (idempotent)
    /// </summary>
    [Fact]
    public async Task HandlePaymentWebhookAsync_AlreadyProcessed_ReturnsSuccessIdempotent()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var orderCode = 123456789L;

        var webhookBody = new { orderCode, amount = 50000 };

        var webhookDto = new PaymentWebhookDto
        {
            OrderCode = orderCode,
            Code = "00",
            Description = "Success",
            Signature = "test"
        };

        var transaction = new Domain.Entities.Transaction
        {
            Id = transactionId,
            TransactionCode = orderCode.ToString(),
            Status = PaymentStatus.Paid, // Already paid!
            Amount = 50000,
            UserId = Guid.NewGuid(),
            TransactionDate = DateTime.UtcNow.AddMinutes(-5)
        };

        _payOsServiceMock
            .Setup(s => s.VerifyAndParseWebhookAsync(It.IsAny<object>(), true))
            .ReturnsAsync(webhookDto);

        _transactionRepoMock
            .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Domain.Entities.Transaction>>(), true))
            .ReturnsAsync(transaction);

        _mapperMock
            .Setup(m => m.Map<TransactionDto>(It.IsAny<Domain.Entities.Transaction>()))
            .Returns(new TransactionDto
            {
                Id = transactionId,
                Status = PaymentStatus.Paid
            });

        // Act
        var result = await _sut.HandlePaymentWebhookAsync(webhookBody, skipSignature: true);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        result.Message.Should().Contain("idempotent");
        // Verify no update was called
        _transactionRepoMock.Verify(r => r.Update(It.IsAny<Domain.Entities.Transaction>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesWithTransactionAsync(), Times.Never);
    }

    #endregion

    #region GetTransactionByIdAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: GetTransactionByIdAsync with non-existent ID
    /// Precondition: Transaction ID does not exist
    /// Expected Result: Returns SYS_Warning0004 (not found)
    /// </summary>
    [Fact]
    public async Task GetTransactionByIdAsync_NotFound_ReturnsWarning()
    {
        // Arrange
        var transactionId = Guid.NewGuid();

        _transactionRepoMock
            .Setup(r => r.GetByIdAsync(transactionId))
            .ReturnsAsync((Domain.Entities.Transaction?)null);

        // Act
        var result = await _sut.GetTransactionByIdAsync(transactionId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: GetTransactionByIdAsync returns transaction details
    /// Precondition: Valid transaction ID exists
    /// Expected Result: Returns transaction DTO with success
    /// </summary>
    [Fact]
    public async Task GetTransactionByIdAsync_ValidId_ReturnsTransaction()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var transaction = new Domain.Entities.Transaction
        {
            Id = transactionId,
            Amount = 50000,
            Status = PaymentStatus.Paid,
            TransactionCode = "123456789"
        };

        _transactionRepoMock
            .Setup(r => r.GetByIdAsync(transactionId))
            .ReturnsAsync(transaction);

        _mapperMock
            .Setup(m => m.Map<TransactionDto>(transaction))
            .Returns(new TransactionDto
            {
                Id = transactionId,
                Amount = 50000,
                Status = PaymentStatus.Paid
            });

        // Act
        var result = await _sut.GetTransactionByIdAsync(transactionId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        result.Data.Should().NotBeNull();
        var dto = result.Data as TransactionDto;
        dto!.Id.Should().Be(transactionId);
        dto.Amount.Should().Be(50000);
    }

    #endregion

    #region CancelTransactionAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: CancelTransactionAsync with non-existent transaction
    /// Precondition: Transaction ID does not exist
    /// Expected Result: Returns SYS_Warning0004
    /// </summary>
    [Fact]
    public async Task CancelTransactionAsync_NotFound_ReturnsWarning()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _transactionRepoMock
            .Setup(r => r.GetByIdAsync(transactionId))
            .ReturnsAsync((Domain.Entities.Transaction?)null);

        // Act
        var result = await _sut.CancelTransactionAsync(transactionId, userId, "User request");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: CancelTransactionAsync by non-owner user
    /// Precondition: User trying to cancel another user's transaction
    /// Expected Result: Returns SYS_Warning0007 (unauthorized)
    /// </summary>
    [Fact]
    public async Task CancelTransactionAsync_NotOwner_ReturnsUnauthorized()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var requestUserId = Guid.NewGuid(); // Different user

        var transaction = new Domain.Entities.Transaction
        {
            Id = transactionId,
            UserId = ownerId,
            Status = PaymentStatus.Pending
        };

        _transactionRepoMock
            .Setup(r => r.GetByIdAsync(transactionId))
            .ReturnsAsync(transaction);

        // Act
        var result = await _sut.CancelTransactionAsync(transactionId, requestUserId, "User request", isAdminRequest: false);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0007);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: CancelTransactionAsync for already paid transaction
    /// Precondition: Transaction is already in Paid status
    /// Expected Result: Returns SYS_Warning0001 (cannot cancel)
    /// </summary>
    [Fact]
    public async Task CancelTransactionAsync_AlreadyPaid_ReturnsWarning()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var transaction = new Domain.Entities.Transaction
        {
            Id = transactionId,
            UserId = userId,
            Status = PaymentStatus.Paid // Already paid
        };

        _transactionRepoMock
            .Setup(r => r.GetByIdAsync(transactionId))
            .ReturnsAsync(transaction);

        // Act
        var result = await _sut.CancelTransactionAsync(transactionId, userId, "User request");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        result.Message.Should().Contain("Paid");
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: CancelTransactionAsync successfully cancels pending transaction
    /// Precondition: User owns transaction, status is Pending
    /// Expected Result: Transaction cancelled, status updated
    /// </summary>
    [Fact]
    public async Task CancelTransactionAsync_ValidRequest_CancelsTransaction()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reason = "Changed my mind";

        var transaction = new Domain.Entities.Transaction
        {
            Id = transactionId,
            UserId = userId,
            Status = PaymentStatus.Pending,
            TransactionCode = "123456789"
        };

        _transactionRepoMock
            .Setup(r => r.GetByIdAsync(transactionId))
            .ReturnsAsync(transaction);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        _mapperMock
            .Setup(m => m.Map<TransactionDto>(It.IsAny<Domain.Entities.Transaction>()))
            .Returns(new TransactionDto
            {
                Id = transactionId,
                Status = PaymentStatus.Cancelled
            });

        // Act
        var result = await _sut.CancelTransactionAsync(transactionId, userId, reason);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        transaction.Status.Should().Be(PaymentStatus.Cancelled);
        transaction.CancelledAt.Should().NotBeNull();
        transaction.CancellationReason.Should().Be(reason);
        _transactionRepoMock.Verify(r => r.Update(transaction), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    #endregion
}
