using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
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
using SFARS.Infrastructure.Hubs;
using AiInferenceEntity = SFARS.Domain.Entities.AiInference;

namespace SFARS.Tests.Application.Services.AiInference;

/// <summary>
/// Unit tests for AiInferenceService:
/// - AnalyzeAsync  (POST /api/ai/analyze)
///
/// Scope: Application service layer — AI inference pipeline for snake detection.
/// Pipeline: Upload → YOLO Detection (Stage 1) → Crop & Pad → EfficientNetV2 (Stage 2) → Save.
/// </summary>
public class AiInferenceServiceTests
{
    private readonly Mock<ISystemMessageService> _msgServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<AiInferenceService>> _loggerMock;
    private readonly Mock<ISnakeDetectionService> _detectionServiceMock;
    private readonly Mock<ISpeciesClassificationService> _classificationServiceMock;
    private readonly Mock<IWoundDetectionService> _woundDetectionServiceMock;
    private readonly Mock<IFileStorageService> _storageServiceMock;
    private readonly Mock<IOptions<StorageOptions>> _storageOptionsMock;
    private readonly Mock<IOptions<YoloDetectionOptions>> _yoloOptionsMock;
    private readonly Mock<IOptions<WoundDetectionOptions>> _woundOptionsMock;
    private readonly Mock<IGenericRepository<Incident, Guid>> _incidentRepoMock;
    private readonly Mock<IGenericRepository<IncidentMedia, Guid>> _mediaRepoMock;
    private readonly Mock<IGenericRepository<AiInferenceEntity, Guid>> _inferenceRepoMock;
    private readonly Mock<IGenericRepository<AiInferenceCandidate, Guid>> _candidateRepoMock;
    private readonly Mock<IGenericRepository<Snake, Guid>> _snakeRepoMock;
    private readonly Mock<IGenericRepository<FirstAidDetail, Guid>> _firstAidRepoMock;
    private readonly Mock<Hangfire.IBackgroundJobClient> _backgroundJobClientMock;

    private readonly AiInferenceService _sut;

    // Valid 1x1 PNG for all image-based tests
    private static readonly byte[] ValidPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==");

    public AiInferenceServiceTests()
    {
        _msgServiceMock = new Mock<ISystemMessageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<AiInferenceService>>();
        _detectionServiceMock = new Mock<ISnakeDetectionService>();
        _classificationServiceMock = new Mock<ISpeciesClassificationService>();
        _woundDetectionServiceMock = new Mock<IWoundDetectionService>();
        _storageServiceMock = new Mock<IFileStorageService>();
        _storageOptionsMock = new Mock<IOptions<StorageOptions>>();
        _yoloOptionsMock = new Mock<IOptions<YoloDetectionOptions>>();
        _woundOptionsMock = new Mock<IOptions<WoundDetectionOptions>>();
        _incidentRepoMock = new Mock<IGenericRepository<Incident, Guid>>();
        _mediaRepoMock = new Mock<IGenericRepository<IncidentMedia, Guid>>();
        _inferenceRepoMock = new Mock<IGenericRepository<AiInferenceEntity, Guid>>();
        _candidateRepoMock = new Mock<IGenericRepository<AiInferenceCandidate, Guid>>();
        _snakeRepoMock = new Mock<IGenericRepository<Snake, Guid>>();
        _firstAidRepoMock = new Mock<IGenericRepository<FirstAidDetail, Guid>>();


        _unitOfWorkMock.Setup(x => x.Repository<Incident, Guid>()).Returns(_incidentRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<IncidentMedia, Guid>()).Returns(_mediaRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<AiInferenceEntity, Guid>()).Returns(_inferenceRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<AiInferenceCandidate, Guid>()).Returns(_candidateRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<Snake, Guid>()).Returns(_snakeRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<FirstAidDetail, Guid>()).Returns(_firstAidRepoMock.Object);


        var storageOptions = new StorageOptions
        {
            MaxUploadBytes = 10 * 1024 * 1024, // 10MB
            AllowedImageTypes = new[] { "image/jpeg", "image/png", "image/jpg" },
            IncidentMediaFolderFormat = "incidents/{0}/media"
        };
        _storageOptionsMock.Setup(x => x.Value).Returns(storageOptions);

        var yoloOptions = new YoloDetectionOptions { MarginRatio = 0.15f };
        _yoloOptionsMock.Setup(x => x.Value).Returns(yoloOptions);

        var woundOptions = new WoundDetectionOptions { ModelName = "wound-cascade-v1", ModelVersion = "1.0.0", MarginRatio = 0.15f };
        _woundOptionsMock.Setup(x => x.Value).Returns(woundOptions);

        _msgServiceMock
            .Setup(x => x.GetMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((string code) => $"Message for {code}");

        // Default empty list mocks for common repository calls
        _firstAidRepoMock.Setup(r => r.GetAllAsync(false)).ReturnsAsync(new List<FirstAidDetail>());
        _snakeRepoMock.Setup(r => r.GetAllAsync(false)).ReturnsAsync(new List<Snake>());

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        _backgroundJobClientMock = new Mock<Hangfire.IBackgroundJobClient>();

        _sut = new AiInferenceService(
            _msgServiceMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object,
            _detectionServiceMock.Object,
            _classificationServiceMock.Object,
            _woundDetectionServiceMock.Object,
            _storageServiceMock.Object,
            _storageOptionsMock.Object,
            _yoloOptionsMock.Object,
            _woundOptionsMock.Object,
            _backgroundJobClientMock.Object,
            new Mock<IHubContext<RescueDispatchHub>>().Object,
            new Mock<IHubContext<LocationTrackingHub>>().Object,
            new Mock<IFcmPushService>().Object
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
        var incidentId = Guid.NewGuid();

        // Act
        var result = await _sut.AnalyzeAsync(
            Guid.Empty, incidentId, null, null, null, null);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0013);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync with empty incident ID
    /// Precondition: incidentId is Guid.Empty
    /// Expected Result: Returns SYS_Warning0001
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_EmptyIncidentId_ReturnsSysWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, Guid.Empty, null, null, null, null);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync when incident not found in database
    /// Precondition: Incident does not exist
    /// Expected Result: Returns SYS_Warning0002
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_IncidentNotFound_ReturnsSysWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        _incidentRepoMock.Setup(r => r.GetByIdAsync(incidentId)).ReturnsAsync((Incident?)null);

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, null, null, null, null);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync when user is not the victim of the incident
    /// Precondition: incident.VictimId != userId
    /// Expected Result: Returns SYS_Warning0007
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_UserNotOwner_ReturnsForbiddenWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = Guid.NewGuid(), // Different user
            CurrentStatus = IncidentStatus.Pending
        };

        _incidentRepoMock.Setup(r => r.GetByIdAsync(incidentId)).ReturnsAsync(incident);

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, null, null, null, null);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0007);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync when incident is closed
    /// Precondition: incident.CurrentStatus = Closed
    /// Expected Result: Returns Incident_Warning0003
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ClosedIncident_ReturnsWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Closed
        };

        _incidentRepoMock.Setup(r => r.GetByIdAsync(incidentId)).ReturnsAsync(incident);

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, null, null, null, null);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Incident_Warning0003);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync when file exceeds max upload size
    /// Precondition: fileSize > MaxUploadBytes
    /// Expected Result: Returns SYS_Warning0008
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

        var imageStream = new MemoryStream(new byte[100]);
        var fileSize = 20 * 1024 * 1024L; // 20MB > 10MB limit

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.jpg", "image/jpeg", fileSize);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0008);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync when file type is not allowed
    /// Precondition: contentType is "application/pdf" (not in allowed list)
    /// Expected Result: Returns AI_Warning0004
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_InvalidFileType_ReturnsFileTypeWarning()
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

        var imageStream = new MemoryStream(new byte[100]);
        var fileSize = 1024L;

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.pdf", "application/pdf", fileSize);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.AI_Warning0004);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync when YOLO detection finds no snake
    /// Precondition: YOLO returns IsDetected=false
    /// Expected Result: Returns success with "Not Snake" status, classifier NOT called
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_YoloNoSnakeDetected_ReturnsNotSnakeResult()
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

        var imageStream = new MemoryStream(ValidPngBytes);
        var fileSize = 1024L;

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new FileUploadResult("https://example.com/image.jpg", "publicId", 1024L, "jpg"));

        // YOLO says NO snake
        _detectionServiceMock
            .Setup(d => d.DetectAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(new SnakeDetectionResult(false, 0f, null));

        // Fallback says NO wound (avoids NullReferenceException on line 210)
        _woundDetectionServiceMock
            .Setup(w => w.DetectWoundAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(new WoundDetectionResult(false, 0f, null));

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.jpg", "image/jpeg", fileSize);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.AI_Success0001);
        result.Data.Should().NotBeNull();
        var response = result.Data as AiInferenceResultDto;
        response!.PrimarySnake.Should().BeNull();
        _detectionServiceMock.Verify(d => d.DetectAsync(It.IsAny<byte[]>()), Times.Once);
        _classificationServiceMock.Verify(y => y.InferSpeciesOnlyAsync(It.IsAny<Stream>(), It.IsAny<int>()), Times.Never);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync when YOLO detects snake but classifier returns empty predictions
    /// Precondition: YOLO detects snake, EfficientNet returns empty
    /// Expected Result: Returns success with low-confidence fallback
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ClassifierEmptyPredictions_ReturnsLowConfidenceFallback()
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

        var imageStream = new MemoryStream(ValidPngBytes);
        var fileSize = 1024L;

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new FileUploadResult("https://example.com/image.jpg", "publicId", 1024L, "jpg"));

        // YOLO detects snake
        _detectionServiceMock
            .Setup(d => d.DetectAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(new SnakeDetectionResult(true, 0.80f, new BoundingBox(10, 10, 100, 100)));

        // But classifier returns empty predictions
        _classificationServiceMock
            .Setup(y => y.InferSpeciesOnlyAsync(It.IsAny<Stream>(), It.IsAny<int>()))
            .ReturnsAsync(new List<SpeciesPrediction>());

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.jpg", "image/jpeg", fileSize);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.AI_Success0001);
        result.Data.Should().NotBeNull();
        var response = result.Data as AiInferenceResultDto;
        response!.PrimarySnake.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync when detected snake class not found in database
    /// Precondition: YOLO detects, classifier classifies, but species not in DB
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

        var imageStream = new MemoryStream(ValidPngBytes);
        var fileSize = 1024L;

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new FileUploadResult("https://example.com/image.jpg", "publicId", 1024L, "jpg"));

        _detectionServiceMock
            .Setup(d => d.DetectAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(new SnakeDetectionResult(true, 0.90f, new BoundingBox(10, 10, 100, 100)));

        var predictions = new List<SpeciesPrediction>
        {
            new SpeciesPrediction("unknown_snake", 0.95f, 0)
        };

        _classificationServiceMock
            .Setup(y => y.InferSpeciesOnlyAsync(It.IsAny<Stream>(), It.IsAny<int>()))
            .ReturnsAsync(predictions);

        _snakeRepoMock.Setup(r => r.GetAllAsync(false)).ReturnsAsync(new List<Snake>()); // Empty snake DB

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.jpg", "image/jpeg", fileSize);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.AI_Warning0005);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: AnalyzeAsync full pipeline: YOLO → Crop → EfficientNet → DB
    /// Precondition: YOLO detects snake, classifier succeeds, snake in DB
    /// Expected Result: Returns success with inference result
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

        var imageStream = new MemoryStream(ValidPngBytes);
        var fileSize = 1024L;

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new FileUploadResult("https://example.com/image.jpg", "publicId", 1024L, "jpg"));

        // Stage 1: YOLO detects snake
        _detectionServiceMock
            .Setup(d => d.DetectAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(new SnakeDetectionResult(true, 0.90f, new BoundingBox(50, 50, 200, 200)));

        // Stage 2: EfficientNetV2 classification
        var predictions = new List<SpeciesPrediction>
        {
            new SpeciesPrediction("naja_kaouthia", 0.95f, 8),
            new SpeciesPrediction("bungarus_candidus", 0.02f, 1)
        };

        _classificationServiceMock
            .Setup(y => y.InferSpeciesOnlyAsync(It.IsAny<Stream>(), It.IsAny<int>()))
            .ReturnsAsync(predictions);

        _snakeRepoMock.Setup(r => r.GetAllAsync(false)).ReturnsAsync(new List<Snake> { snake });

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.jpg", "image/jpeg", fileSize);

        // Assert
        result.Data.Should().NotBeNull();
        _detectionServiceMock.Verify(d => d.DetectAsync(It.IsAny<byte[]>()), Times.Once);
        _classificationServiceMock.Verify(y => y.InferSpeciesOnlyAsync(It.IsAny<Stream>(), 3), Times.Once);
        _mediaRepoMock.Verify(r => r.AddAsync(It.IsAny<IncidentMedia>()), Times.Once);
        _inferenceRepoMock.Verify(r => r.AddAsync(It.IsAny<AiInferenceEntity>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: AnalyzeAsync with skipped upload (no image provided)
    /// Precondition: imageStream is null (skip mode)
    /// Expected Result: Returns success without AI calls
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_SkipMode_ReturnsSuccessWithoutAiCalls()
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

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, null, null, null, null); // Skip mode

        // Assert
        result.Data.Should().NotBeNull();
        _detectionServiceMock.Verify(d => d.DetectAsync(It.IsAny<byte[]>()), Times.Never);
        _classificationServiceMock.Verify(y => y.InferSpeciesOnlyAsync(It.IsAny<Stream>(), It.IsAny<int>()), Times.Never);
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
        var imageStream = new MemoryStream(ValidPngBytes);

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new FileUploadResult("https://example.com/image.jpg", "publicId", 1024L, "jpg"));

        _detectionServiceMock
            .Setup(d => d.DetectAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(new SnakeDetectionResult(true, 0.80f, new BoundingBox(0, 0, 50, 50)));

        var predictions = new List<SpeciesPrediction>
        {
            new SpeciesPrediction("naja_kaouthia", 0.85f, 8)
        };

        _classificationServiceMock
            .Setup(y => y.InferSpeciesOnlyAsync(It.IsAny<Stream>(), It.IsAny<int>()))
            .ReturnsAsync(predictions);

        _snakeRepoMock.Setup(r => r.GetAllAsync(false)).ReturnsAsync(new List<Snake> { snake });

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.jpg", "image/jpeg", maxFileSize);

        // Assert
        result.Data.Should().NotBeNull();
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: AnalyzeAsync with multiple predictions (top-K)
    /// Precondition: YOLO detects snake, classifier returns 3 predictions
    /// Expected Result: All 3 candidates saved, highest confidence selected
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_MultiplePredictions_SavesAllCandidates()
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

        var imageStream = new MemoryStream(ValidPngBytes);
        var fileSize = 1024L;

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new FileUploadResult("https://example.com/image.jpg", "publicId", 1024L, "jpg"));

        _detectionServiceMock
            .Setup(d => d.DetectAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(new SnakeDetectionResult(true, 0.85f, new BoundingBox(10, 10, 200, 200)));

        var predictions = new List<SpeciesPrediction>
        {
            new SpeciesPrediction("naja_kaouthia", 0.85f, 8),
            new SpeciesPrediction("ophiophagus_hannah", 0.10f, 10),
            new SpeciesPrediction("bungarus_candidus", 0.05f, 1)
        };

        _classificationServiceMock
            .Setup(y => y.InferSpeciesOnlyAsync(It.IsAny<Stream>(), It.IsAny<int>()))
            .ReturnsAsync(predictions);

        _snakeRepoMock.Setup(r => r.GetAllAsync(false)).ReturnsAsync(snakes);

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.jpg", "image/jpeg", fileSize);

        // Assert
        result.Data.Should().NotBeNull();
        _candidateRepoMock.Verify(r => r.AddAsync(It.IsAny<AiInferenceCandidate>()), Times.Exactly(3));
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: AnalyzeAsync when classifier identifies object as "Not Snake"
    /// Precondition: YOLO detects object, classifier top-1 = "not_snake"
    /// Expected Result: Returns success with Not Snake status
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ClassifierSaysNotSnake_ReturnsNotSnakeResult()
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

        var imageStream = new MemoryStream(ValidPngBytes);
        var fileSize = 1024L;

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new FileUploadResult("https://example.com/image.jpg", "publicId", 1024L, "jpg"));

        _detectionServiceMock
            .Setup(d => d.DetectAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(new SnakeDetectionResult(true, 0.60f, new BoundingBox(10, 10, 100, 100)));

        // Classifier says it's Not_Snake (class index 9)
        var predictions = new List<SpeciesPrediction>
        {
            new SpeciesPrediction("not_snake", 0.90f, 9),
            new SpeciesPrediction("naja_kaouthia", 0.05f, 8)
        };

        _classificationServiceMock
            .Setup(y => y.InferSpeciesOnlyAsync(It.IsAny<Stream>(), It.IsAny<int>()))
            .ReturnsAsync(predictions);

        // Act
        var result = await _sut.AnalyzeAsync(
            userId, incidentId, imageStream, "test.jpg", "image/jpeg", fileSize);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.AI_Success0001);
        result.Data.Should().NotBeNull();
        var response = result.Data as AiInferenceResultDto;
        response!.PrimarySnake.Should().BeNull();
    }

    #endregion
}
