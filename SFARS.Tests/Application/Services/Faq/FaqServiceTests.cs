using FluentAssertions;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Faq;
using SFARS.Application.Services.Faq;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.Tests.Application.Services.Faq;

/// <summary>
/// Unit tests for FaqService:
/// - CreateFaqAsync            (POST /api/admin/faqs)
/// - UpdateFaqAsync            (PUT  /api/admin/faqs/{id})
/// - DeleteFaqAsync            (DELETE /api/admin/faqs/{id})
/// - GetFaqByIdAsync           (GET  /api/faqs/{id})
/// - GetAllActiveFaqsAsync     (GET  /api/faqs)
/// - GetAllFaqsPaginatedAsync  (GET  /api/admin/faqs)
///
/// Scope: Application service layer - Admin CRUD operations and Public read operations.
/// </summary>
public class FaqServiceTests
{
    private readonly Mock<ISystemMessageService> _msgServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<ILogger<FaqService>> _loggerMock;
    private readonly Mock<IGenericRepository<Domain.Entities.Faq, Guid>> _faqRepoMock;

    private readonly FaqService _sut;

    public FaqServiceTests()
    {
        _msgServiceMock = new Mock<ISystemMessageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mapperMock = new Mock<IMapper>();
        _loggerMock = new Mock<ILogger<FaqService>>();
        _faqRepoMock = new Mock<IGenericRepository<Domain.Entities.Faq, Guid>>();

        _unitOfWorkMock.Setup(x => x.Repository<Domain.Entities.Faq, Guid>()).Returns(_faqRepoMock.Object);

        _msgServiceMock
            .Setup(x => x.GetMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((string code) => $"Message for {code}");

        _sut = new FaqService(
            _msgServiceMock.Object,
            _unitOfWorkMock.Object,
            _mapperMock.Object,
            _loggerMock.Object
        );
    }

    #region CreateFaqAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: CreateFaqAsync with empty Question
    /// Precondition: DTO has null or whitespace Question
    /// Expected Result: Returns SYS_Warning0001 (validation error)
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateFaqAsync_EmptyQuestion_ReturnsValidationWarning(string? question)
    {
        // Arrange
        var dto = new FaqDto
        {
            Question = question!,
            Answer = "Sample answer"
        };

        // Act
        var result = await _sut.CreateFaqAsync(dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: CreateFaqAsync with empty Answer
    /// Precondition: DTO has null or whitespace Answer
    /// Expected Result: Returns SYS_Warning0001 (validation error)
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateFaqAsync_EmptyAnswer_ReturnsValidationWarning(string? answer)
    {
        // Arrange
        var dto = new FaqDto
        {
            Question = "What is SFARS?",
            Answer = answer!
        };

        // Act
        var result = await _sut.CreateFaqAsync(dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: CreateFaqAsync with duplicate Question
    /// Precondition: FAQ with same Question (case-insensitive) already exists
    /// Expected Result: Returns SYS_Warning0003 (duplicate entry)
    /// </summary>
    [Fact]
    public async Task CreateFaqAsync_DuplicateQuestion_ReturnsDuplicateWarning()
    {
        // Arrange
        var dto = new FaqDto
        {
            Question = "What is SFARS?",
            Answer = "Snake First Aid Response System"
        };

        _faqRepoMock
            .Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Faq, bool>>>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.CreateFaqAsync(dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0003);
        result.Message.Should().Contain("already exists");
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: CreateFaqAsync with valid data and auto-assigned Order
    /// Precondition: DTO has Order = 0, no existing FAQs
    /// Expected Result: Returns success with Order = 1
    /// </summary>
    [Fact]
    public async Task CreateFaqAsync_ValidDataNoOrder_AutoAssignsOrderOne()
    {
        // Arrange
        var dto = new FaqDto
        {
            Question = "What is SFARS?",
            Answer = "Snake First Aid Response System",
            Order = 0,
            IsActive = true
        };

        var createdEntity = new Domain.Entities.Faq
        {
            Id = Guid.NewGuid(),
            Question = dto.Question,
            Answer = dto.Answer,
            Order = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var resultDto = new FaqDto
        {
            Id = createdEntity.Id,
            Question = createdEntity.Question,
            Answer = createdEntity.Answer,
            Order = createdEntity.Order,
            IsActive = createdEntity.IsActive,
            CreatedAt = createdEntity.CreatedAt,
            UpdatedAt = createdEntity.UpdatedAt ?? DateTime.UtcNow
        };

        _faqRepoMock
            .Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Faq, bool>>>()))
            .ReturnsAsync(false);

        _faqRepoMock
            .Setup(r => r.GetAllAsync(true))
            .ReturnsAsync(new List<Domain.Entities.Faq>());

        _mapperMock.Setup(m => m.Map<Domain.Entities.Faq>(It.IsAny<FaqDto>())).Returns(createdEntity);
        _mapperMock.Setup(m => m.Map<FaqDto>(It.IsAny<Domain.Entities.Faq>())).Returns(resultDto);

        _faqRepoMock.Setup(r => r.AddAsync(It.IsAny<Domain.Entities.Faq>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.CreateFaqAsync(dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        result.Data.Should().NotBeNull();
        var data = result.Data as FaqDto;
        data!.Order.Should().Be(1);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: CreateFaqAsync with valid data and existing FAQs
    /// Precondition: DTO has Order = 0, 3 existing FAQs with max Order = 5
    /// Expected Result: Returns success with Order = 6
    /// </summary>
    [Fact]
    public async Task CreateFaqAsync_ValidDataWithExistingFaqs_AutoAssignsNextOrder()
    {
        // Arrange
        var dto = new FaqDto
        {
            Question = "What is SFARS?",
            Answer = "Snake First Aid Response System",
            Order = 0,
            IsActive = true
        };

        var existingFaqs = new List<Domain.Entities.Faq>
        {
            new Domain.Entities.Faq { Id = Guid.NewGuid(), Order = 1 },
            new Domain.Entities.Faq { Id = Guid.NewGuid(), Order = 3 },
            new Domain.Entities.Faq { Id = Guid.NewGuid(), Order = 5 }
        };

        var createdEntity = new Domain.Entities.Faq
        {
            Id = Guid.NewGuid(),
            Question = dto.Question,
            Answer = dto.Answer,
            Order = 6,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var resultDto = new FaqDto
        {
            Id = createdEntity.Id,
            Order = 6
        };

        _faqRepoMock
            .Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Faq, bool>>>()))
            .ReturnsAsync(false);

        _faqRepoMock
            .Setup(r => r.GetAllAsync(true))
            .ReturnsAsync(existingFaqs);

        _mapperMock.Setup(m => m.Map<Domain.Entities.Faq>(It.IsAny<FaqDto>())).Returns(createdEntity);
        _mapperMock.Setup(m => m.Map<FaqDto>(It.IsAny<Domain.Entities.Faq>())).Returns(resultDto);

        _faqRepoMock.Setup(r => r.AddAsync(It.IsAny<Domain.Entities.Faq>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.CreateFaqAsync(dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        var data = result.Data as FaqDto;
        data!.Order.Should().Be(6);
    }

    #endregion

    #region UpdateFaqAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: UpdateFaqAsync with non-existent ID
    /// Precondition: FAQ ID does not exist in database
    /// Expected Result: Returns SYS_Warning0004 (not found)
    /// </summary>
    [Fact]
    public async Task UpdateFaqAsync_NotFound_ReturnsWarning()
    {
        // Arrange
        var faqId = Guid.NewGuid();
        var dto = new FaqDto
        {
            Question = "Updated question",
            Answer = "Updated answer"
        };

        _faqRepoMock
            .Setup(r => r.GetByIdAsync(faqId))
            .ReturnsAsync((Domain.Entities.Faq?)null);

        // Act
        var result = await _sut.UpdateFaqAsync(faqId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        result.Message.Should().Contain("not found");
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: UpdateFaqAsync with empty Question
    /// Precondition: DTO has null or whitespace Question
    /// Expected Result: Returns SYS_Warning0001 (validation error)
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateFaqAsync_EmptyQuestion_ReturnsValidationWarning(string? question)
    {
        // Arrange
        var faqId = Guid.NewGuid();
        var existingFaq = new Domain.Entities.Faq
        {
            Id = faqId,
            Question = "Original question",
            Answer = "Original answer"
        };

        var dto = new FaqDto
        {
            Question = question!,
            Answer = "Updated answer"
        };

        _faqRepoMock
            .Setup(r => r.GetByIdAsync(faqId))
            .ReturnsAsync(existingFaq);

        // Act
        var result = await _sut.UpdateFaqAsync(faqId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: UpdateFaqAsync with duplicate Question (excluding current FAQ)
    /// Precondition: Another FAQ with same Question already exists
    /// Expected Result: Returns SYS_Warning0003 (duplicate entry)
    /// </summary>
    [Fact]
    public async Task UpdateFaqAsync_DuplicateQuestion_ReturnsDuplicateWarning()
    {
        // Arrange
        var faqId = Guid.NewGuid();
        var existingFaq = new Domain.Entities.Faq
        {
            Id = faqId,
            Question = "Original question",
            Answer = "Original answer"
        };

        var dto = new FaqDto
        {
            Question = "Duplicate question",
            Answer = "Updated answer"
        };

        _faqRepoMock
            .Setup(r => r.GetByIdAsync(faqId))
            .ReturnsAsync(existingFaq);

        _faqRepoMock
            .Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Faq, bool>>>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.UpdateFaqAsync(faqId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0003);
        result.Message.Should().Contain("already exists");
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: UpdateFaqAsync with valid data
    /// Precondition: Valid FAQ ID and valid DTO data
    /// Expected Result: Returns success with updated FAQ
    /// </summary>
    [Fact]
    public async Task UpdateFaqAsync_ValidData_ReturnsSuccess()
    {
        // Arrange
        var faqId = Guid.NewGuid();
        var existingFaq = new Domain.Entities.Faq
        {
            Id = faqId,
            Question = "Original question",
            Answer = "Original answer",
            Order = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var dto = new FaqDto
        {
            Question = "Updated question",
            Answer = "Updated answer",
            Order = 2,
            IsActive = false
        };

        var resultDto = new FaqDto
        {
            Id = faqId,
            Question = dto.Question,
            Answer = dto.Answer,
            Order = dto.Order,
            IsActive = dto.IsActive,
            UpdatedAt = DateTime.UtcNow
        };

        _faqRepoMock
            .Setup(r => r.GetByIdAsync(faqId))
            .ReturnsAsync(existingFaq);

        _faqRepoMock
            .Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Faq, bool>>>()))
            .ReturnsAsync(false);

        _mapperMock.Setup(m => m.Map<FaqDto>(It.IsAny<Domain.Entities.Faq>())).Returns(resultDto);

        _faqRepoMock.Setup(r => r.Update(It.IsAny<Domain.Entities.Faq>()));
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.UpdateFaqAsync(faqId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        result.Message.Should().Contain("updated successfully");
        result.Data.Should().NotBeNull();
    }

    #endregion

    #region DeleteFaqAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: DeleteFaqAsync with non-existent ID
    /// Precondition: FAQ ID does not exist in database
    /// Expected Result: Returns SYS_Warning0004 (not found)
    /// </summary>
    [Fact]
    public async Task DeleteFaqAsync_NotFound_ReturnsWarning()
    {
        // Arrange
        var faqId = Guid.NewGuid();

        _faqRepoMock
            .Setup(r => r.GetByIdAsync(faqId))
            .ReturnsAsync((Domain.Entities.Faq?)null);

        // Act
        var result = await _sut.DeleteFaqAsync(faqId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        result.Message.Should().Contain("not found");
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: DeleteFaqAsync with valid ID (soft delete)
    /// Precondition: Valid FAQ ID exists
    /// Expected Result: Returns success and IsActive set to false
    /// </summary>
    [Fact]
    public async Task DeleteFaqAsync_ValidId_SoftDeletesAndReturnsSuccess()
    {
        // Arrange
        var faqId = Guid.NewGuid();
        var existingFaq = new Domain.Entities.Faq
        {
            Id = faqId,
            Question = "Question to delete",
            Answer = "Answer to delete",
            IsActive = true
        };

        _faqRepoMock
            .Setup(r => r.GetByIdAsync(faqId))
            .ReturnsAsync(existingFaq);

        _faqRepoMock.Setup(r => r.Update(It.IsAny<Domain.Entities.Faq>()));
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.DeleteFaqAsync(faqId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        result.Message.Should().Contain("deleted successfully");
        existingFaq.IsActive.Should().BeFalse();
        _faqRepoMock.Verify(r => r.Update(It.Is<Domain.Entities.Faq>(f => f.IsActive == false)), Times.Once);
    }

    #endregion

    #region GetFaqByIdAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: GetFaqByIdAsync with non-existent ID
    /// Precondition: FAQ ID does not exist in database
    /// Expected Result: Returns SYS_Warning0004 (not found)
    /// </summary>
    [Fact]
    public async Task GetFaqByIdAsync_NotFound_ReturnsWarning()
    {
        // Arrange
        var faqId = Guid.NewGuid();

        _faqRepoMock
            .Setup(r => r.GetByIdAsync(faqId))
            .ReturnsAsync((Domain.Entities.Faq?)null);

        // Act
        var result = await _sut.GetFaqByIdAsync(faqId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        result.Message.Should().Contain("not found");
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: GetFaqByIdAsync with valid ID
    /// Precondition: Valid FAQ ID exists
    /// Expected Result: Returns success with FAQ data
    /// </summary>
    [Fact]
    public async Task GetFaqByIdAsync_ValidId_ReturnsFaq()
    {
        // Arrange
        var faqId = Guid.NewGuid();
        var faq = new Domain.Entities.Faq
        {
            Id = faqId,
            Question = "What is SFARS?",
            Answer = "Snake First Aid Response System",
            Order = 1,
            IsActive = true
        };

        var faqDto = new FaqDto
        {
            Id = faqId,
            Question = faq.Question,
            Answer = faq.Answer,
            Order = faq.Order,
            IsActive = faq.IsActive
        };

        _faqRepoMock
            .Setup(r => r.GetByIdAsync(faqId))
            .ReturnsAsync(faq);

        _mapperMock.Setup(m => m.Map<FaqDto>(faq)).Returns(faqDto);

        // Act
        var result = await _sut.GetFaqByIdAsync(faqId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        result.Data.Should().NotBeNull();
        var data = result.Data as FaqDto;
        data!.Id.Should().Be(faqId);
        data.Question.Should().Be("What is SFARS?");
    }

    #endregion

    #region GetAllActiveFaqsAsync Tests

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: GetAllActiveFaqsAsync returns only active FAQs sorted by Order
    /// Precondition: Database has 5 FAQs, 3 active and 2 inactive
    /// Expected Result: Returns 3 active FAQs sorted by Order ascending
    /// </summary>
    [Fact]
    public async Task GetAllActiveFaqsAsync_MixedFaqs_ReturnsOnlyActiveSorted()
    {
        // Arrange
        var allFaqs = new List<Domain.Entities.Faq>
        {
            new Domain.Entities.Faq { Id = Guid.NewGuid(), Question = "Q1", Order = 5, IsActive = true },
            new Domain.Entities.Faq { Id = Guid.NewGuid(), Question = "Q2", Order = 1, IsActive = true },
            new Domain.Entities.Faq { Id = Guid.NewGuid(), Question = "Q3", Order = 10, IsActive = false },
            new Domain.Entities.Faq { Id = Guid.NewGuid(), Question = "Q4", Order = 3, IsActive = true },
            new Domain.Entities.Faq { Id = Guid.NewGuid(), Question = "Q5", Order = 2, IsActive = false }
        };

        var activeFaqDtos = new List<FaqDto>
        {
            new FaqDto { Question = "Q2", Order = 1, IsActive = true },
            new FaqDto { Question = "Q4", Order = 3, IsActive = true },
            new FaqDto { Question = "Q1", Order = 5, IsActive = true }
        };

        _faqRepoMock
            .Setup(r => r.GetAllAsync(true))
            .ReturnsAsync(allFaqs);

        _mapperMock.Setup(m => m.Map<List<FaqDto>>(It.IsAny<List<Domain.Entities.Faq>>()))
            .Returns(activeFaqDtos);

        // Act
        var result = await _sut.GetAllActiveFaqsAsync();

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        result.Data.Should().NotBeNull();
        var data = result.Data as List<FaqDto>;
        data.Should().HaveCount(3);
        data![0].Question.Should().Be("Q2"); // Order 1
        data[1].Question.Should().Be("Q4"); // Order 3
        data[2].Question.Should().Be("Q1"); // Order 5
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: GetAllActiveFaqsAsync with no FAQs
    /// Precondition: Database is empty
    /// Expected Result: Returns empty list with success
    /// </summary>
    [Fact]
    public async Task GetAllActiveFaqsAsync_NoFaqs_ReturnsEmptyList()
    {
        // Arrange
        _faqRepoMock
            .Setup(r => r.GetAllAsync(true))
            .ReturnsAsync(new List<Domain.Entities.Faq>());

        _mapperMock.Setup(m => m.Map<List<FaqDto>>(It.IsAny<List<Domain.Entities.Faq>>()))
            .Returns(new List<FaqDto>());

        // Act
        var result = await _sut.GetAllActiveFaqsAsync();

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        result.Data.Should().NotBeNull();
        var data = result.Data as List<FaqDto>;
        data.Should().BeEmpty();
    }

    #endregion

    #region GetAllFaqsPaginatedAsync Tests

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: GetAllFaqsPaginatedAsync with pagination
    /// Precondition: Database has 15 FAQs, requesting page 1 with size 10
    /// Expected Result: Returns 10 items with correct pagination metadata
    /// </summary>
    [Fact]
    public async Task GetAllFaqsPaginatedAsync_Page1Size10_ReturnsCorrectPage()
    {
        // Arrange
        var allFaqs = Enumerable.Range(1, 15)
            .Select(i => new Domain.Entities.Faq
            {
                Id = Guid.NewGuid(),
                Question = $"Question {i}",
                Answer = $"Answer {i}",
                Order = i,
                IsActive = true
            })
            .ToList();

        var pageItems = allFaqs.Skip(10).Take(10).ToList();
        var pageDtos = pageItems.Select(f => new FaqDto
        {
            Id = f.Id,
            Question = f.Question,
            Order = f.Order
        }).ToList();

        _faqRepoMock
            .Setup(r => r.GetAllAsync(true))
            .ReturnsAsync(allFaqs);

        _mapperMock.Setup(m => m.Map<List<FaqDto>>(It.IsAny<List<Domain.Entities.Faq>>()))
            .Returns(pageDtos);

        // Act
        var result = await _sut.GetAllFaqsPaginatedAsync(pageIndex: 1, pageSize: 10);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        result.Data.Should().NotBeNull();

        var dataType = result.Data!.GetType();
        var itemsProperty = dataType.GetProperty("Items");
        var totalCountProperty = dataType.GetProperty("TotalCount");
        var pageIndexProperty = dataType.GetProperty("PageIndex");
        var pageSizeProperty = dataType.GetProperty("PageSize");
        var totalPagesProperty = dataType.GetProperty("TotalPages");

        var items = itemsProperty!.GetValue(result.Data) as List<FaqDto>;
        var totalCount = (int)totalCountProperty!.GetValue(result.Data)!;
        var pageIndex = (int)pageIndexProperty!.GetValue(result.Data)!;
        var pageSize = (int)pageSizeProperty!.GetValue(result.Data)!;
        var totalPages = (int)totalPagesProperty!.GetValue(result.Data)!;

        items.Should().HaveCount(5); // Remaining items on page 1
        totalCount.Should().Be(15);
        pageIndex.Should().Be(1);
        pageSize.Should().Be(10);
        totalPages.Should().Be(2);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: GetAllFaqsPaginatedAsync default parameters
    /// Precondition: Database has FAQs, no pagination params provided
    /// Expected Result: Returns first 10 items (default page 0, size 10)
    /// </summary>
    [Fact]
    public async Task GetAllFaqsPaginatedAsync_DefaultParams_ReturnsFirstPage()
    {
        // Arrange
        var allFaqs = Enumerable.Range(1, 25)
            .Select(i => new Domain.Entities.Faq
            {
                Id = Guid.NewGuid(),
                Question = $"Question {i}",
                Order = i,
                IsActive = i % 2 == 0 // Mix of active/inactive
            })
            .ToList();

        var firstPageDtos = allFaqs.Take(10).Select(f => new FaqDto
        {
            Question = f.Question,
            Order = f.Order
        }).ToList();

        _faqRepoMock
            .Setup(r => r.GetAllAsync(true))
            .ReturnsAsync(allFaqs);

        _mapperMock.Setup(m => m.Map<List<FaqDto>>(It.IsAny<List<Domain.Entities.Faq>>()))
            .Returns(firstPageDtos);

        // Act
        var result = await _sut.GetAllFaqsPaginatedAsync();

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);

        var dataType = result.Data!.GetType();
        var itemsProperty = dataType.GetProperty("Items");
        var totalCountProperty = dataType.GetProperty("TotalCount");
        var pageIndexProperty = dataType.GetProperty("PageIndex");
        var totalPagesProperty = dataType.GetProperty("TotalPages");

        var items = itemsProperty!.GetValue(result.Data) as List<FaqDto>;
        var totalCount = (int)totalCountProperty!.GetValue(result.Data)!;
        var pageIndex = (int)pageIndexProperty!.GetValue(result.Data)!;
        var totalPages = (int)totalPagesProperty!.GetValue(result.Data)!;

        items.Should().HaveCount(10);
        totalCount.Should().Be(25);
        pageIndex.Should().Be(0);
        totalPages.Should().Be(3); // 25 / 10 = 2.5 -> 3 pages
    }

    #endregion
}
