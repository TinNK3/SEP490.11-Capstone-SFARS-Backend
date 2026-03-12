using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Dtos.AiInference;
using SFARS.Application.Services;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;
using SFARS.Infrastructure.Configurations;
using AiInferenceEntity = SFARS.Domain.Entities.AiInference;

namespace SFARS.Tests.Application.Services.AiInference;

/// <summary>
/// Unit tests for AiInferenceService:
/// - AnalyzeAsync  (POST /api/ai/analyze)
///
/// Scope: Application service layer - AI inference pipeline for snake detection.
/// Pipeline: Upload → YOLO inference → DB lookup → First-aid retrieval → Save in 1 transaction.
/// </summary>
public class AiInferenceServiceTests
{
    private readonly Mock<ISystemMessageService> _msgServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<AiInferenceService>> _loggerMock;
    private readonly Mock<IYoloInferenceService> _yoloServiceMock;
    private readonly Mock<IFileStorageService> _storageServiceMock;
    private readonly Mock<IOptions<StorageOptions>> _storageOptionsMock;
    private readonly Mock<IGenericRepository<Incident, Guid>> _incidentRepoMock;
    private readonly Mock<IGenericRepository<IncidentMedia, Guid>> _mediaRepoMock;
    private readonly Mock<IGenericRepository<AiInferenceEntity, Guid>> _inferenceRepoMock;
    private readonly Mock<IGenericRepository<AiInferenceCandidate, Guid>> _candidateRepoMock;
    private readonly Mock<IGenericRepository<Snake, Guid>> _snakeRepoMock;
    private readonly Mock<IGenericRepository<FirstAidDetail, Guid>> _firstAidRepoMock;
    private readonly Mock<IGenericRepository<IncidentChat, Guid>> _chatRepoMock;
    private readonly Mock<IGenericRepository<IncidentChatMessage, Guid>> _chatMessageRepoMock;
    private readonly Mock<Hangfire.IBackgroundJobClient> _backgroundJobClientMock;

    private readonly AiInferenceService _sut;

    public AiInferenceServiceTests()
    {
        _msgServiceMock = new Mock<ISystemMessageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<AiInferenceService>>();
        _yoloServiceMock = new Mock<IYoloInferenceService>();
        _storageServiceMock = new Mock<IFileStorageService>();
        _storageOptionsMock = new Mock<IOptions<StorageOptions>>();
        _incidentRepoMock = new Mock<IGenericRepository<Incident, Guid>>();
        _mediaRepoMock = new Mock<IGenericRepository<IncidentMedia, Guid>>();
        _inferenceRepoMock = new Mock<IGenericRepository<AiInferenceEntity, Guid>>();
        _candidateRepoMock = new Mock<IGenericRepository<AiInferenceCandidate, Guid>>();
        _snakeRepoMock = new Mock<IGenericRepository<Snake, Guid>>();
        _firstAidRepoMock = new Mock<IGenericRepository<FirstAidDetail, Guid>>();
        _chatRepoMock = new Mock<IGenericRepository<IncidentChat, Guid>>();
        _chatMessageRepoMock = new Mock<IGenericRepository<IncidentChatMessage, Guid>>();

        _unitOfWorkMock.Setup(x => x.Repository<Incident, Guid>()).Returns(_incidentRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<IncidentMedia, Guid>()).Returns(_mediaRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<AiInferenceEntity, Guid>()).Returns(_inferenceRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<AiInferenceCandidate, Guid>()).Returns(_candidateRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<Snake, Guid>()).Returns(_snakeRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<FirstAidDetail, Guid>()).Returns(_firstAidRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<IncidentChat, Guid>()).Returns(_chatRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<IncidentChatMessage, Guid>()).Returns(_chatMessageRepoMock.Object);

        var storageOptions = new StorageOptions
        {
            MaxUploadBytes = 10 * 1024 * 1024, // 10MB
            AllowedImageTypes = new[] { "image/jpeg", "image/png", "image/jpg" },
            IncidentMediaFolderFormat = "incidents/{0}/media"
        };
        _storageOptionsMock.Setup(x => x.Value).Returns(storageOptions);

        _msgServiceMock
            .Setup(x => x.GetMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((string code) => $"Message for {code}");

        _backgroundJobClientMock = new Mock<Hangfire.IBackgroundJobClient>();

        _sut = new AiInferenceService(
            _msgServiceMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object,
            _yoloServiceMock.Object,
            _storageServiceMock.Object,
            _storageOptionsMock.Object,
            _backgroundJobClientMock.Object
        );
    }

    #region AnalyzeAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync with empty user ID
    /// Precondition: userId is Guid.Empty
    /// Expected Result: Returns Auth_Warning0013
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_EmptyUserId_ReturnsAuthWarning()
    {
        // Arrange
        var userId = Guid.Empty;
        var incidentId = Guid.NewGuid();

        // Act
        var result = await _sut.AnalyzeAsync(userId, incidentId, null, null, null, null, null);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0013);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync with empty incident ID
    /// Precondition: incidentId is Guid.Empty
    /// Expected Result: Returns SYS_Warning0001
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_EmptyIncidentId_ReturnsValidationWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.Empty;

        // Act
        var result = await _sut.AnalyzeAsync(userId, incidentId, null, null, null, null, null);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync with non-existent incident
    /// Precondition: Valid IDs but incident does not exist
    /// Expected Result: Returns SYS_Warning0002 (not found)
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_IncidentNotFound_ReturnsNotFoundWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();

        _incidentRepoMock.Setup(r => r.GetByIdAsync(incidentId)).ReturnsAsync((Incident?)null);

        // Act
        var result = await _sut.AnalyzeAsync(userId, incidentId, null, null, null, null, null);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync when user is not the incident owner
    /// Precondition: Incident exists but VictimId != userId
    /// Expected Result: Returns SYS_Warning0007 (unauthorized)
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_UnauthorizedUser_ReturnsUnauthorizedWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = otherUserId, // Different user!
            CurrentStatus = IncidentStatus.Pending
        };

        _incidentRepoMock.Setup(r => r.GetByIdAsync(incidentId)).ReturnsAsync(incident);

        // Act
        var result = await _sut.AnalyzeAsync(userId, incidentId, null, null, null, null, null);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0007);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync on closed or cancelled incident
    /// Precondition: Incident status is Closed or Cancelled
    /// Expected Result: Returns Incident_Warning0003 (incident closed)
    /// </summary>
    [Theory]
    [InlineData(IncidentStatus.Closed)]
    [InlineData(IncidentStatus.Cancelled)]
    public async Task AnalyzeAsync_ClosedIncident_ReturnsClosedWarning(IncidentStatus status)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = status // Closed or Cancelled
        };

        _incidentRepoMock.Setup(r => r.GetByIdAsync(incidentId)).ReturnsAsync(incident);

        // Act
        var result = await _sut.AnalyzeAsync(userId, incidentId, null, null, null, null, null);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Incident_Warning0003);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: AnalyzeAsync with file size exceeding limit
    /// Precondition: File size > MaxUploadBytes (10MB)
    /// Expected Result: Returns SYS_Warning0008 (file too large)
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_FileTooLarge_ReturnsFileSizeWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        _incidentRepoMock.Setup(r => r.GetByIdAsync(incidentId)).ReturnsAsync(incident);

        var largeFileSize = 11 * 1024 * 1024L; // 11MB > 10MB limit
        var imageStream = new MemoryStream(new byte[1024]);

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.jpg", "image/jpeg", largeFileSize, MediaType.SnakePhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0008);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync with invalid image type
    /// Precondition: ContentType is not in AllowedImageTypes
    /// Expected Result: Returns AI_Warning0004 (invalid image type)
    /// </summary>
    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("video/mp4")]
    public async Task AnalyzeAsync_InvalidImageType_ReturnsInvalidTypeWarning(string contentType)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        _incidentRepoMock.Setup(r => r.GetByIdAsync(incidentId)).ReturnsAsync(incident);

        var imageStream = new MemoryStream(new byte[1024]);
        var fileSize = 1024L;

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.file", contentType, fileSize, MediaType.SnakePhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.AI_Warning0004);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync when YOLO returns no predictions
    /// Precondition: Valid image but YOLO does not detect any snakes
    /// Expected Result: Returns AI_Warning0001 (no snake detected)
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_NoSnakeDetected_ReturnsNoDetectionWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        _incidentRepoMock.Setup(r => r.GetByIdAsync(incidentId)).ReturnsAsync(incident);

        var imageStream = new MemoryStream(new byte[1024]);
        var fileSize = 1024L;

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new FileUploadResult("https://example.com/image.jpg", "publicId", 1024L, "jpg"));

        _yoloServiceMock
            .Setup(y => y.InferCascadedAsync(It.IsAny<Stream>(), It.IsAny<int>()))
            .ReturnsAsync(new YoloPipelineResult(true, 1f, new List<YoloPrediction>())); // Empty list!

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.jpg", "image/jpeg", fileSize, MediaType.SnakePhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.AI_Warning0001);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync when detected snake class not found in database
    /// Precondition: YOLO returns prediction but snake class not in DB
    /// Expected Result: Returns AI_Warning0005 (snake not found in database)
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_SnakeClassNotInDatabase_ReturnsNotFoundWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        _incidentRepoMock.Setup(r => r.GetByIdAsync(incidentId)).ReturnsAsync(incident);

        var imageStream = new MemoryStream(new byte[1024]);
        var fileSize = 1024L;

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new FileUploadResult("https://example.com/image.jpg", "publicId", 1024L, "jpg"));

        var predictions = new List<YoloPrediction>
        {
            new YoloPrediction("unknown_snake", 0.95f, 0)
        };

        _yoloServiceMock
            .Setup(y => y.InferCascadedAsync(It.IsAny<Stream>(), It.IsAny<int>()))
            .ReturnsAsync(new YoloPipelineResult(true, 1f, predictions));

        _snakeRepoMock.Setup(r => r.GetAllAsync(false)).ReturnsAsync(new List<Snake>()); // Empty snake DB

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.jpg", "image/jpeg", fileSize, MediaType.SnakePhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.AI_Warning0005);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: AnalyzeAsync successfully processes image and saves inference
    /// Precondition: Valid image, YOLO detects snake, snake found in DB
    /// Expected Result: Returns success with inference result and first-aid recommendations
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ValidImage_ReturnsSuccessWithInference()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        var snakeId = Guid.NewGuid();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        var snake = new Snake
        {
            Id = snakeId,
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra",
            ToxinGroup = ToxinGroup.Neurotoxin,
            ToxicityLevel = SnakeRiskLevel.Deadly,
            IsActive = true
        };

        _incidentRepoMock.Setup(r => r.GetByIdAsync(incidentId)).ReturnsAsync(incident);

        var imageStream = new MemoryStream(new byte[1024]);
        var fileSize = 1024L;

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new FileUploadResult("https://example.com/image.jpg", "publicId", 1024L, "jpg"));

        var predictions = new List<YoloPrediction>
        {
            new YoloPrediction("naja_kaouthia", 0.95f, 0),
            new YoloPrediction("bungarus_candidus", 0.02f, 1)
        };

        _yoloServiceMock
            .Setup(y => y.InferCascadedAsync(It.IsAny<Stream>(), It.IsAny<int>()))
            .ReturnsAsync(new YoloPipelineResult(true, 1f, predictions));

        _snakeRepoMock.Setup(r => r.GetAllAsync(false)).ReturnsAsync(new List<Snake> { snake });
        _firstAidRepoMock.Setup(r => r.GetAllAsync(false)).ReturnsAsync(new List<FirstAidDetail>());
        _chatRepoMock.Setup(r => r.GetAllAsync(true)).ReturnsAsync(new List<IncidentChat>());

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.jpg", "image/jpeg", fileSize, MediaType.SnakePhoto);

        // Assert
        result.Data.Should().NotBeNull();
        _mediaRepoMock.Verify(r => r.AddAsync(It.IsAny<IncidentMedia>()), Times.Once);
        _inferenceRepoMock.Verify(r => r.AddAsync(It.IsAny<AiInferenceEntity>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: AnalyzeAsync with skipped upload (no image provided)
    /// Precondition: imageStream is null (skip mode)
    /// Expected Result: Returns success with skipped inference (no YOLO call)
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_SkipMode_ReturnsSuccessWithoutYolo()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        _incidentRepoMock.Setup(r => r.GetByIdAsync(incidentId)).ReturnsAsync(incident);
        _firstAidRepoMock.Setup(r => r.GetAllAsync(false)).ReturnsAsync(new List<FirstAidDetail>());

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, null, null, null, null, null); // Skip mode

        // Assert
        result.Data.Should().NotBeNull();
        _yoloServiceMock.Verify(y => y.InferCascadedAsync(It.IsAny<Stream>(), It.IsAny<int>()), Times.Never);
        _storageServiceMock.Verify(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _inferenceRepoMock.Verify(r => r.AddAsync(It.IsAny<AiInferenceEntity>()), Times.Once);
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: AnalyzeAsync with exactly max file size
    /// Precondition: File size = MaxUploadBytes (10MB)
    /// Expected Result: Returns success (boundary accepted)
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ExactMaxFileSize_ProcessesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        var snakeId = Guid.NewGuid();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        var snake = new Snake
        {
            Id = snakeId,
            ScientificName = "Naja kaouthia",
            CommonName = "Monocled cobra",
            ToxinGroup = ToxinGroup.Neurotoxin,
            IsActive = true
        };

        _incidentRepoMock.Setup(r => r.GetByIdAsync(incidentId)).ReturnsAsync(incident);

        var maxFileSize = 10 * 1024 * 1024L; // Exactly 10MB
        var imageStream = new MemoryStream(new byte[1024]);

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new FileUploadResult("https://example.com/image.jpg", "publicId", 1024L, "jpg"));

        var predictions = new List<YoloPrediction>
        {
            new YoloPrediction("naja_kaouthia", 0.85f, 0)
        };

        _yoloServiceMock
            .Setup(y => y.InferCascadedAsync(It.IsAny<Stream>(), It.IsAny<int>()))
            .ReturnsAsync(new YoloPipelineResult(true, 1f, predictions));

        _snakeRepoMock.Setup(r => r.GetAllAsync(false)).ReturnsAsync(new List<Snake> { snake });
        _firstAidRepoMock.Setup(r => r.GetAllAsync(false)).ReturnsAsync(new List<FirstAidDetail>());
        _chatRepoMock.Setup(r => r.GetAllAsync(true)).ReturnsAsync(new List<IncidentChat>());

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.jpg", "image/jpeg", maxFileSize, MediaType.SnakePhoto);

        // Assert
        result.Data.Should().NotBeNull();
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: AnalyzeAsync with multiple YOLO predictions (top-K)
    /// Precondition: YOLO returns 3 predictions (top-K=3)
    /// Expected Result: All 3 candidates saved, highest confidence selected
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_MultipleYoloPredictions_SavesAllCandidates()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        var snakes = new List<Snake>
        {
            new Snake { Id = Guid.NewGuid(), ScientificName = "Naja kaouthia", CommonName = "Monocled cobra", ToxinGroup = ToxinGroup.Neurotoxin, IsActive = true },
            new Snake { Id = Guid.NewGuid(), ScientificName = "Ophiophagus hannah", CommonName = "King cobra", ToxinGroup = ToxinGroup.Neurotoxin, IsActive = true },
            new Snake { Id = Guid.NewGuid(), ScientificName = "Bungarus candidus", CommonName = "Malayan krait", ToxinGroup = ToxinGroup.Neurotoxin, IsActive = true }
        };

        _incidentRepoMock.Setup(r => r.GetByIdAsync(incidentId)).ReturnsAsync(incident);

        var imageStream = new MemoryStream(new byte[1024]);
        var fileSize = 1024L;

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new FileUploadResult("https://example.com/image.jpg", "publicId", 1024L, "jpg"));

        var predictions = new List<YoloPrediction>
        {
            new YoloPrediction("naja_kaouthia", 0.85f, 0),
            new YoloPrediction("ophiophagus_hannah", 0.10f, 1),
            new YoloPrediction("bungarus_candidus", 0.05f, 2)
        };

        _yoloServiceMock
            .Setup(y => y.InferCascadedAsync(It.IsAny<Stream>(), It.IsAny<int>()))
            .ReturnsAsync(new YoloPipelineResult(true, 1f, predictions));

        _snakeRepoMock.Setup(r => r.GetAllAsync(false)).ReturnsAsync(snakes);
        _firstAidRepoMock.Setup(r => r.GetAllAsync(false)).ReturnsAsync(new List<FirstAidDetail>());
        _chatRepoMock.Setup(r => r.GetAllAsync(true)).ReturnsAsync(new List<IncidentChat>());

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.jpg", "image/jpeg", fileSize, MediaType.SnakePhoto);

        // Assert
        result.Data.Should().NotBeNull();
        _candidateRepoMock.Verify(r => r.AddAsync(It.IsAny<AiInferenceCandidate>()), Times.Exactly(3));
    }

    #endregion
}
