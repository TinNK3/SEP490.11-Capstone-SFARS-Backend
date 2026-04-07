using FluentAssertions;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Application.Services;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Interfaces;
using SFARS.Domain.Specifications.Params;
using System.IO;

namespace SFARS.Tests.Application.Services.Snakes;

/// <summary>
/// Unit tests for SnakeService:
/// - GetSnakeById         (GET  /api/snakes/{id})
/// - CreateAsync          (POST /api/snakes)
/// - UpdateSnakeAsync     (PUT  /api/snakes/{id})
/// - DeleteSnake          (DELETE /api/snakes/{id})
/// - GetAllSnakesAsync    (GET  /api/snakes)
/// - PreviewImportAsync   (POST /api/snakes/import/preview)
/// - ApplyImportAsync     (POST /api/snakes/import/apply)
///
/// Scope: Application service layer with field-level change detection and audit logging.
/// </summary>
public class SnakeServiceTests
{
    private readonly Mock<ISystemMessageService> _msgServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<ILogger<SnakeService>> _loggerMock;
    private readonly Mock<IGenericRepository<Snake, Guid>> _snakeRepoMock;
    private readonly Mock<IGenericRepository<SnakeImage, Guid>> _imageRepoMock;
    private readonly Mock<IGenericRepository<SnakeChangeLog, Guid>> _changeLogRepoMock;
    private readonly Mock<IFileStorageService> _storageServiceMock;

    private readonly SnakeService _sut;

    public SnakeServiceTests()
    {
        _msgServiceMock = new Mock<ISystemMessageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mapperMock = new Mock<IMapper>();
        _loggerMock = new Mock<ILogger<SnakeService>>();
        _snakeRepoMock = new Mock<IGenericRepository<Snake, Guid>>();
        _imageRepoMock = new Mock<IGenericRepository<SnakeImage, Guid>>();
        _changeLogRepoMock = new Mock<IGenericRepository<SnakeChangeLog, Guid>>();
        _storageServiceMock = new Mock<IFileStorageService>();

        _unitOfWorkMock.Setup(x => x.Repository<Snake, Guid>()).Returns(_snakeRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<SnakeImage, Guid>()).Returns(_imageRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<SnakeChangeLog, Guid>()).Returns(_changeLogRepoMock.Object);

        _msgServiceMock
            .Setup(x => x.GetMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((string code) => $"Message for {code}");

        _sut = new SnakeService(
            _msgServiceMock.Object,
            _unitOfWorkMock.Object,
            _mapperMock.Object,
            _loggerMock.Object,
            _storageServiceMock.Object
        );
    }

    #region GetSnakeById Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: GetSnakeById with non-existent ID
    /// Precondition: Snake ID does not exist in database
    /// Expected Result: Returns SYS_Warning0004 (not found)
    /// </summary>
    [Fact]
    public async Task GetSnakeById_NotFound_ReturnsWarning()
    {
        // Arrange
        var snakeId = Guid.NewGuid();

        _snakeRepoMock
            .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Snake>>(), It.IsAny<bool>()))
            .ReturnsAsync((Snake?)null);

        // Act
        var result = await _sut.GetSnakeById(snakeId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: GetSnakeById returns snake data with images
    /// Precondition: Valid snake ID exists
    /// Expected Result: Returns snake DTO with success and images
    /// </summary>
    [Fact]
    public async Task GetSnakeById_ValidId_ReturnsSnake()
    {
        // Arrange
        var snakeId = Guid.NewGuid();
        var snake = new Snake
        {
            Id = snakeId,
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra",
            ToxinGroup = ToxinGroup.Neurotoxin,
            ToxicityLevel = SnakeRiskLevel.Deadly,
            IsActive = true,
            SnakeImages = new List<SnakeImage>
            {
                new SnakeImage { Id = Guid.NewGuid(), ImageUrl = "url1", IsPrimary = true }
            }
        };

        var snakeDto = new SnakeDto
        {
            Id = snakeId,
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra",
            SnakeImages = new List<SnakeImageDto>
            {
                new SnakeImageDto { ImageUrl = "url1", IsPrimary = true }
            }
        };

        _snakeRepoMock
            .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Snake>>(), It.IsAny<bool>()))
            .ReturnsAsync(snake);

        _mapperMock.Setup(m => m.Map<SnakeDto>(snake)).Returns(snakeDto);

        // Act
        var result = await _sut.GetSnakeById(snakeId);

        // Assert
        result.ResultCode.Should().NotBe(ResultCodeConst.SYS_Warning0004);
        var returnedDto = (SnakeDto)result.Data!;
        returnedDto.SnakeImages.Should().NotBeEmpty();
        returnedDto.SnakeImages.First().ImageUrl.Should().Be("url1");
    }

    #endregion

    #region CreateAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: CreateAsync with empty ScientificName
    /// Precondition: DTO has null or whitespace ScientificName
    /// Expected Result: Returns SYS_Warning0001 (validation error)
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_EmptyScientificName_ReturnsValidationWarning(string? scientificName)
    {
        // Arrange
        var dto = new SnakeDto
        {
            ScientificName = scientificName!,
            CommonName = "Test Snake"
        };

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: CreateAsync with empty CommonName
    /// Precondition: DTO has null or whitespace CommonName
    /// Expected Result: Returns SYS_Warning0001 (validation error)
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_EmptyCommonName_ReturnsValidationWarning(string? commonName)
    {
        // Arrange
        var dto = new SnakeDto
        {
            ScientificName = "Naja kaouthia",
            CommonName = commonName!
        };

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: CreateAsync with duplicate ScientificName
    /// Precondition: Snake with same ScientificName already exists
    /// Expected Result: Returns SYS_Warning0003 (duplicate entry)
    /// </summary>
    [Fact]
    public async Task CreateAsync_DuplicateScientificName_ReturnsDuplicateWarning()
    {
        // Arrange
        var dto = new SnakeDto
        {
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra"
        };

        _snakeRepoMock
            .Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Snake, bool>>>()))
            .ReturnsAsync(true); // Snake already exists

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0003);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: CreateAsync successfully creates snake with audit log
    /// Precondition: Valid DTO with unique ScientificName
    /// Expected Result: Returns SYS_Success0001 with snake created and audit logs saved
    /// </summary>
    [Fact]
    public async Task CreateAsync_ValidDto_CreatesSnake()
    {
        // Arrange
        var dto = new SnakeDto
        {
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra",
            ToxinGroup = ToxinGroup.Neurotoxin,
            ToxicityLevel = SnakeRiskLevel.Deadly
        };

        var snake = new Snake
        {
            Id = Guid.NewGuid(),
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra",
            ToxinGroup = ToxinGroup.Neurotoxin,
            ToxicityLevel = SnakeRiskLevel.Deadly
        };

        _snakeRepoMock
            .Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Snake, bool>>>()))
            .ReturnsAsync(false); // No duplicate

        _mapperMock.Setup(m => m.Map<Snake>(dto)).Returns(snake);
        _mapperMock.Setup(m => m.Map<SnakeDto>(snake)).Returns(dto);

        _snakeRepoMock
            .Setup(r => r.GetAllAsync(false))
            .ReturnsAsync(new List<Snake> { snake });

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        _snakeRepoMock.Verify(r => r.AddAsync(It.IsAny<Snake>()), Times.Once);
        _changeLogRepoMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<SnakeChangeLog>>()), Times.Once);
    }

    #endregion

    #region UpdateSnakeAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: UpdateSnakeAsync with non-existent snake ID
    /// Precondition: Snake ID does not exist
    /// Expected Result: Returns SYS_Warning0004 (not found)
    /// </summary>
    [Fact]
    public async Task UpdateSnakeAsync_NotFound_ReturnsWarning()
    {
        // Arrange
        var snakeId = Guid.NewGuid();
        var dto = new SnakeDto { ScientificName = "Test", CommonName = "Test" };

        _snakeRepoMock.Setup(r => r.GetByIdAsync(snakeId)).ReturnsAsync((Snake?)null);

        // Act
        var result = await _sut.UpdateSnakeAsync(snakeId, dto, null);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: UpdateSnakeAsync modifying sensitive fields without changeReason
    /// Precondition: ToxicityLevel or ToxinGroup changed but changeReason is null/empty
    /// Expected Result: Returns SYS_Warning0001 (changeReason required)
    /// </summary>
    [Fact]
    public async Task UpdateSnakeAsync_SensitiveFieldWithoutReason_ReturnsWarning()
    {
        // Arrange
        var snakeId = Guid.NewGuid();
        var existingSnake = new Snake
        {
            Id = snakeId,
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra",
            ToxicityLevel = SnakeRiskLevel.HighlyVenomous,
            ToxinGroup = ToxinGroup.Neurotoxin
        };

        var dto = new SnakeDto
        {
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra",
            ToxicityLevel = SnakeRiskLevel.Deadly, // Changed!
            ToxinGroup = ToxinGroup.Neurotoxin
        };

        _snakeRepoMock.Setup(r => r.GetByIdAsync(snakeId)).ReturnsAsync(existingSnake);

        // Act
        var result = await _sut.UpdateSnakeAsync(snakeId, dto, null); // No changeReason

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        result.Message.Should().Contain("ChangeReason is required");
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: UpdateSnakeAsync with no actual changes
    /// Precondition: DTO values match existing entity
    /// Expected Result: Returns SYS_Success0003 with no database updates
    /// </summary>
    [Fact]
    public async Task UpdateSnakeAsync_NoChanges_ReturnsSuccessWithoutUpdate()
    {
        // Arrange
        var snakeId = Guid.NewGuid();
        var existingSnake = new Snake
        {
            Id = snakeId,
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra",
            ToxicityLevel = SnakeRiskLevel.HighlyVenomous,
            ToxinGroup = ToxinGroup.Neurotoxin,
            Description = "A venomous snake"
        };

        var dto = new SnakeDto
        {
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra",
            ToxicityLevel = SnakeRiskLevel.HighlyVenomous,
            ToxinGroup = ToxinGroup.Neurotoxin,
            Description = "A venomous snake"
        };

        _snakeRepoMock.Setup(r => r.GetByIdAsync(snakeId)).ReturnsAsync(existingSnake);

        // Act
        var result = await _sut.UpdateSnakeAsync(snakeId, dto, "Test reason");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
        result.Data.Should().Be(true);
        _snakeRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Snake>()), Times.Never);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: UpdateSnakeAsync successfully updates non-sensitive fields
    /// Precondition: Valid update with non-sensitive field changes
    /// Expected Result: Returns SYS_Success0003 with changes saved and audit logged
    /// </summary>
    [Fact]
    public async Task UpdateSnakeAsync_NonSensitiveFields_UpdatesSuccessfully()
    {
        // Arrange
        var snakeId = Guid.NewGuid();
        var existingSnake = new Snake
        {
            Id = snakeId,
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra",
            ToxicityLevel = SnakeRiskLevel.HighlyVenomous,
            ToxinGroup = ToxinGroup.Neurotoxin,
            Description = "Old description"
        };

        var dto = new SnakeDto
        {
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra",
            ToxicityLevel = SnakeRiskLevel.HighlyVenomous,
            ToxinGroup = ToxinGroup.Neurotoxin,
            Description = "New description" // Changed!
        };

        _snakeRepoMock.Setup(r => r.GetByIdAsync(snakeId)).ReturnsAsync(existingSnake);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.UpdateSnakeAsync(snakeId, dto, null); // No reason needed for non-sensitive

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
        result.Data.Should().Be(true);
        _snakeRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Snake>()), Times.Once);
        _changeLogRepoMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<SnakeChangeLog>>()), Times.Once);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: UpdateSnakeAsync with sensitive fields and valid changeReason
    /// Precondition: Sensitive fields changed with valid changeReason
    /// Expected Result: Returns SYS_Success0003 with warning message about sensitive changes
    /// </summary>
    [Fact]
    public async Task UpdateSnakeAsync_SensitiveFieldsWithReason_UpdatesWithWarning()
    {
        // Arrange
        var snakeId = Guid.NewGuid();
        var existingSnake = new Snake
        {
            Id = snakeId,
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra",
            ToxicityLevel = SnakeRiskLevel.HighlyVenomous,
            ToxinGroup = ToxinGroup.Neurotoxin
        };

        var dto = new SnakeDto
        {
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra",
            ToxicityLevel = SnakeRiskLevel.Deadly, // Changed!
            ToxinGroup = ToxinGroup.Neurotoxin
        };

        _snakeRepoMock.Setup(r => r.GetByIdAsync(snakeId)).ReturnsAsync(existingSnake);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.UpdateSnakeAsync(snakeId, dto, "Updated based on new research");

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0003);
        result.Message.Should().Contain("Sensitive fields changed");
        _snakeRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Snake>()), Times.Once);
    }

    #endregion

    #region DeleteSnake Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: DeleteSnake with non-existent ID
    /// Precondition: Snake ID does not exist
    /// Expected Result: Returns SYS_Warning0004 (not found)
    /// </summary>
    [Fact]
    public async Task DeleteSnake_NotFound_ReturnsWarning()
    {
        // Arrange
        var snakeId = Guid.NewGuid();
        _snakeRepoMock.Setup(r => r.GetByIdAsync(snakeId)).ReturnsAsync((Snake?)null);

        // Act
        var result = await _sut.DeleteSnake(snakeId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: DeleteSnake performs soft delete with audit log
    /// Precondition: Valid snake exists
    /// Expected Result: Returns SYS_Success0004, sets IsActive=false, creates audit log
    /// </summary>
    [Fact]
    public async Task DeleteSnake_ValidId_SoftDeletes()
    {
        // Arrange
        var snakeId = Guid.NewGuid();
        var snake = new Snake
        {
            Id = snakeId,
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra",
            IsActive = true
        };

        _snakeRepoMock.Setup(r => r.GetByIdAsync(snakeId)).ReturnsAsync(snake);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.DeleteSnake(snakeId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0004);
        snake.IsActive.Should().BeFalse();
        _snakeRepoMock.Verify(r => r.UpdateAsync(snake), Times.Once);
        _changeLogRepoMock.Verify(r => r.AddAsync(It.Is<SnakeChangeLog>(log =>
            log.FieldName == nameof(Snake.IsActive) &&
            log.OldValue == "true" &&
            log.NewValue == "false")), Times.Once);
    }

    #endregion

    #region GetAllSnakesAsync Tests

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: GetAllSnakesAsync with search term
    /// </summary>
    [Fact]
    public async Task GetAllSnakesAsync_ValidSearchTerm_ReturnsFilteredResults()
    {
        // Arrange
        var specParams = new SnakeSpecParams { Search = "cobra" };
        var snakes = new List<Snake>
        {
            new Snake 
            { 
                Id = Guid.NewGuid(), 
                ScientificName = "Naja kaouthia", 
                CommonName = "Monocled cobra", 
                IsActive = true,
                SnakeImages = new List<SnakeImage> { new SnakeImage { ImageUrl = "url1" } }
            },
            new Snake { Id = Guid.NewGuid(), ScientificName = "Ophiophagus hannah", CommonName = "King cobra", IsActive = true }
        };

        _snakeRepoMock
            .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Snake>>(), It.IsAny<bool>()))
            .ReturnsAsync(snakes);

        _mapperMock.Setup(m => m.Map<IEnumerable<SnakeDto>>(It.IsAny<IEnumerable<Snake>>()))
            .Returns(new List<SnakeDto> 
            { 
                new SnakeDto { SnakeImages = new List<SnakeImageDto>{ new SnakeImageDto() } }, 
                new SnakeDto() 
            });

        // Act
        var result = await _sut.GetAllSnakesAsync(specParams);

        // Assert
        result.Data.Should().NotBeNull();
        var pagedResult = (PaginatedResultDto<SnakeDto>)result.Data!;
        pagedResult.Items.Should().HaveCount(2);
        pagedResult.Items.First().SnakeImages.Should().NotBeNull();
        _snakeRepoMock.Verify(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Snake>>(), It.IsAny<bool>()), Times.Once);
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: GetAllSnakesAsync with null/empty search term
    /// </summary>
    [Fact]
    public async Task GetAllSnakesAsync_EmptySearchTerm_ReturnsAllSnakes()
    {
        // Arrange
        var specParams = new SnakeSpecParams();
        _snakeRepoMock
            .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Snake>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<Snake>());

        _mapperMock.Setup(m => m.Map<IEnumerable<SnakeDto>>(It.IsAny<IEnumerable<Snake>>()))
            .Returns(new List<SnakeDto>());

        // Act
        var result = await _sut.GetAllSnakesAsync(specParams);

        // Assert
        result.Data.Should().NotBeNull();
        var pagedResult = (PaginatedResultDto<SnakeDto>)result.Data!;
        pagedResult.Items.Should().NotBeNull();
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: GetAllSnakesAsync with pagination
    /// </summary>
    [Fact]
    public async Task GetAllSnakesAsync_Pagination_ReturnsCorrectPage()
    {
        // Arrange
        var specParams = new SnakeSpecParams { Search = "test", Page = 1, PageSize = 10 };

        _snakeRepoMock
            .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Snake>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<Snake>());

        _mapperMock.Setup(m => m.Map<IEnumerable<SnakeDto>>(It.IsAny<IEnumerable<Snake>>()))
            .Returns(new List<SnakeDto>());

        // Act
        var result = await _sut.GetAllSnakesAsync(specParams);

        // Assert
        result.Data.Should().NotBeNull();
        var pagedResult = (PaginatedResultDto<SnakeDto>)result.Data!;
        pagedResult.Items.Should().NotBeNull();
        _snakeRepoMock.Verify(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Snake>>(), It.IsAny<bool>()), Times.Once);
    }

    #endregion

    #region UpdateSnakeImagesAsync Tests

    [Fact]
    public async Task UpdateSnakeImagesAsync_SnakeNotFound_ReturnsWarning()
    {
        // Arrange
        var snakeId = Guid.NewGuid();
        _snakeRepoMock
            .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Snake>>(), It.IsAny<bool>()))
            .ReturnsAsync((Snake?)null);

        // Act
        var result = await _sut.UpdateSnakeImagesAsync(snakeId, null, null, null, null);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        result.Message.Should().Contain("Snake not found");
    }

    [Fact]
    public async Task UpdateSnakeImagesAsync_ValidRequest_PerformsUpdatesSuccessfully()
    {
        // Arrange
        var snakeId = Guid.NewGuid();
        var existingImgId1 = Guid.NewGuid();
        var existingImgId2 = Guid.NewGuid(); // To be deleted

        var snake = new Snake
        {
            Id = snakeId,
            SnakeImages = new List<SnakeImage>
            {
                new SnakeImage { Id = existingImgId1, ImageUrl = "url1", IsPrimary = true },
                new SnakeImage { Id = existingImgId2, ImageUrl = "url2", IsPrimary = false }
            }
        };

        _snakeRepoMock
            .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Snake>>(), It.IsAny<bool>()))
            .ReturnsAsync(snake);

        _storageServiceMock
            .Setup(s => s.DeleteByUrlAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new FileUploadResult("new_url", "pub_id", 100, "jpg"));

        var newImages = new List<SnakeImageUploadInfo>
        {
            new SnakeImageUploadInfo(new MemoryStream(), "test.jpg", "image/jpeg")
        };

        // Act: Keep existingImgId1, Delete existingImgId2, Add 1 new image, Set new image as primary
        var result = await _sut.UpdateSnakeImagesAsync(
            snakeId,
            new List<Guid> { existingImgId1 },
            null, // Not choosing existing as primary
            newImages,
            0 // Choosing the 1st new image as primary
        );

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        
        // Verify deletions
        _storageServiceMock.Verify(s => s.DeleteByUrlAsync("url2"), Times.Once);
        _imageRepoMock.Verify(r => r.DeleteAsync(existingImgId2), Times.Once);

        // Verify uploads
        _storageServiceMock.Verify(s => s.UploadAsync(It.IsAny<Stream>(), "test.jpg", "snakes", "image/jpeg"), Times.Once);
        
        // Verify IsPrimary logic
        snake.SnakeImages.Should().HaveCount(2);
        var keptOld = snake.SnakeImages.First(i => i.Id == existingImgId1);
        var newlyAdded = snake.SnakeImages.First(i => i.ImageUrl == "new_url");
        
        keptOld.IsPrimary.Should().BeFalse();
        newlyAdded.IsPrimary.Should().BeTrue();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    #endregion
}
