using FluentAssertions;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Analytics;
using SFARS.Application.Interfaces.Services;
using SFARS.Application.Services;
using SFARS.Application.Services.Analytics;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.Interfaces;
using SFARS.Domain.Specifications.Params;
using System.Collections;
using System.Linq.Expressions;
using AiInferenceEntity = SFARS.Domain.Entities.AiInference;
using AiInferenceReviewEntity = SFARS.Domain.Entities.AiInferenceReview;

namespace SFARS.Tests.Application.Services.Analytics;

public class AnalyticsServiceTests
{
    private readonly Mock<IGenericRepository<Incident, Guid>> _incidentRepoMock;
    private readonly Mock<IGenericRepository<RescueMission, Guid>> _rescueRepoMock;
    private readonly Mock<IGenericRepository<Domain.Entities.Transaction, Guid>> _transactionRepoMock;
    private readonly Mock<IGenericRepository<User, Guid>> _userRepoMock;
    private readonly Mock<IGenericRepository<AiInferenceEntity, Guid>> _aiRepoMock;
    private readonly Mock<IGenericRepository<AiInferenceReviewEntity, Guid>> _aiReviewRepoMock;
    private readonly Mock<IGenericRepository<UserDevice, Guid>> _deviceRepoMock;
    private readonly Mock<ISystemMessageService> _msgServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ISpeciesClassificationService> _speciesServiceMock;
    private readonly AnalyticsService _sut;

    public AnalyticsServiceTests()
    {
        _incidentRepoMock = new Mock<IGenericRepository<Incident, Guid>>();
        _rescueRepoMock = new Mock<IGenericRepository<RescueMission, Guid>>();
        _transactionRepoMock = new Mock<IGenericRepository<Domain.Entities.Transaction, Guid>>();
        _userRepoMock = new Mock<IGenericRepository<User, Guid>>();
        _aiRepoMock = new Mock<IGenericRepository<AiInferenceEntity, Guid>>();
        _aiReviewRepoMock = new Mock<IGenericRepository<AiInferenceReviewEntity, Guid>>();
        _deviceRepoMock = new Mock<IGenericRepository<UserDevice, Guid>>();
        _msgServiceMock = new Mock<ISystemMessageService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _speciesServiceMock = new Mock<ISpeciesClassificationService>();

        _msgServiceMock
            .Setup(x => x.GetMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((string code) => $"Message for {code}");

        _incidentRepoMock.Setup(x => x.GetQueryable(It.IsAny<bool>())).Returns(ToAsyncQueryable(new List<Incident>()));
        _rescueRepoMock.Setup(x => x.GetQueryable(It.IsAny<bool>())).Returns(ToAsyncQueryable(new List<RescueMission>()));
        _transactionRepoMock.Setup(x => x.GetQueryable(It.IsAny<bool>())).Returns(ToAsyncQueryable(new List<Domain.Entities.Transaction>()));
        _userRepoMock.Setup(x => x.GetQueryable(It.IsAny<bool>())).Returns(ToAsyncQueryable(new List<User>()));
        _aiRepoMock.Setup(x => x.GetQueryable(It.IsAny<bool>())).Returns(ToAsyncQueryable(new List<AiInferenceEntity>()));
        _aiReviewRepoMock.Setup(x => x.GetQueryable(It.IsAny<bool>())).Returns(ToAsyncQueryable(new List<AiInferenceReviewEntity>()));

        // Wire up UnitOfWork to return the repos
        _unitOfWorkMock
            .Setup(x => x.Repository<UserDevice, Guid>())
            .Returns(_deviceRepoMock.Object);

        _unitOfWorkMock
            .Setup(x => x.Repository<User, Guid>())
            .Returns(_userRepoMock.Object);

        var overviewService = new AnalyticsOverviewService(
            _incidentRepoMock.Object,
            _rescueRepoMock.Object,
            _transactionRepoMock.Object,
            _userRepoMock.Object,
            _aiReviewRepoMock.Object,
            _msgServiceMock.Object);

        var incidentService = new AnalyticsIncidentService(
            _incidentRepoMock.Object,
            _rescueRepoMock.Object,
            _aiReviewRepoMock.Object,
            _msgServiceMock.Object);

        var rescuerService = new AnalyticsRescuerService(
            _rescueRepoMock.Object,
            _userRepoMock.Object,
            _msgServiceMock.Object);

        var aiService = new AnalyticsAiService(
            _aiRepoMock.Object,
            _aiReviewRepoMock.Object,
            _rescueRepoMock.Object,
            _speciesServiceMock.Object,
            _msgServiceMock.Object);

        var exportService = new AnalyticsExportService(
            overviewService,
            incidentService,
            rescuerService,
            aiService,
            _msgServiceMock.Object);

        _sut = new AnalyticsService(
            overviewService,
            incidentService,
            rescuerService,
            aiService,
            exportService,
            _notificationServiceMock.Object,
            _unitOfWorkMock.Object,
            _msgServiceMock.Object);
    }

    [Fact]
    public async Task GetRescuerMissionHistoryAsync_RescuerNotFound_ReturnsWarningNotFound()
    {
        // Arrange
        var rescuerId = Guid.NewGuid();
        var filter = new AnalyticsSpecParams();
        _userRepoMock.Setup(x => x.GetByIdAsync(rescuerId)).ReturnsAsync((User?)null);

        // Act
        var result = await _sut.GetRescuerMissionHistoryAsync(rescuerId, filter);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetRescuerMissionHistoryAsync_NoMissions_ReturnsEmptyHistory()
    {
        // Arrange
        var rescuerId = Guid.NewGuid();
        var filter = new AnalyticsSpecParams();
        var rescuer = new User { Id = rescuerId, FirstName = "Rescuer", LastName = "One", Email = "rescuer@example.com", PasswordHash = "hash" };

        _userRepoMock.Setup(x => x.GetByIdAsync(rescuerId)).ReturnsAsync(rescuer);
        _rescueRepoMock.Setup(x => x.GetQueryable(false)).Returns(ToAsyncQueryable(new List<RescueMission>()));

        // Act
        var result = await _sut.GetRescuerMissionHistoryAsync(rescuerId, filter);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var data = result.Data.Should().BeOfType<RescuerMissionHistoryDto>().Subject;
        data.RescuerId.Should().Be(rescuerId);
        data.RescuerName.Should().Be(rescuer.FullName);
        data.TotalMissions.Should().Be(0);
        data.SuccessRate.Should().Be(0);
        data.Missions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRescuerMissionHistoryAsync_WithMissions_ReturnsMappedMetrics()
    {
        // Arrange
        var rescuerId = Guid.NewGuid();
        var victimId = Guid.NewGuid();
        var incidentId1 = Guid.NewGuid();
        var incidentId2 = Guid.NewGuid();
        var missionId1 = Guid.NewGuid();
        var missionId2 = Guid.NewGuid();
        var created1 = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var created2 = new DateTime(2026, 1, 2, 1, 0, 0, DateTimeKind.Utc);

        var rescuer = new User { Id = rescuerId, FirstName = "A", LastName = "Rescuer", Email = "rescuer@example.com", PasswordHash = "hash" };
        var victim = new User
        {
            Id = victimId,
            FirstName = "B",
            LastName = "Victim",
            Email = "victim@example.com",
            PasswordHash = "hash",
            Phone = "0123",
            Address = "District 1"
        };

        var incident1 = new Incident
        {
            Id = incidentId1,
            Code = "SOS-2026-00001",
            VictimId = victimId,
            Victim = victim,
            Location = new Point(106.7, 10.7) { SRID = 4326 },
            PriorityLevel = SeverityLevel.Critical,
            Description = "Mission one"
        };

        var incident2 = new Incident
        {
            Id = incidentId2,
            Code = "SOS-2026-00002",
            VictimId = victimId,
            Victim = victim,
            Location = new Point(106.8, 10.8) { SRID = 4326 },
            PriorityLevel = SeverityLevel.High,
            Description = "Mission two"
        };

        var mission1 = new RescueMission
        {
            Id = missionId1,
            IncidentId = incidentId1,
            Incident = incident1,
            RescuerId = rescuerId,
            Rescuer = rescuer,
            Status = RescueStatus.Completed,
            CreatedAt = created1,
            StartedAt = created1.AddMinutes(10),
            ArrivedAt = created1.AddMinutes(40),
            CompletedAt = created1.AddMinutes(100),
            RescuerNotes = "Stable",
            PatientConditionAtHandover = "Conscious",

            TrackingLogs = new List<RescueTrackingLog>
            {
                new() { MissionId = missionId1, RescuerId = rescuerId, Location = new Point(106.71, 10.71) { SRID = 4326 }, SpeedKMH = 40 },
                new() { MissionId = missionId1, RescuerId = rescuerId, Location = new Point(106.72, 10.72) { SRID = 4326 }, SpeedKMH = 60 },
                new() { MissionId = missionId1, RescuerId = rescuerId, Location = new Point(106.73, 10.73) { SRID = 4326 }, SpeedKMH = null }
            }
        };

        var mission2 = new RescueMission
        {
            Id = missionId2,
            IncidentId = incidentId2,
            Incident = incident2,
            RescuerId = rescuerId,
            Rescuer = rescuer,
            Status = RescueStatus.Accepted,
            CreatedAt = created2
        };

        var missions = new List<RescueMission> { mission1, mission2 };
        var filter = new AnalyticsSpecParams();

        _userRepoMock.Setup(x => x.GetByIdAsync(rescuerId)).ReturnsAsync(rescuer);
        _rescueRepoMock.Setup(x => x.GetQueryable(false)).Returns(ToAsyncQueryable(missions));

        // Act
        var result = await _sut.GetRescuerMissionHistoryAsync(rescuerId, filter);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var data = result.Data.Should().BeOfType<RescuerMissionHistoryDto>().Subject;
        data.TotalMissions.Should().Be(2);
        data.SuccessRate.Should().Be(0.5d);

        var completedMission = data.Missions.Single(x => x.MissionId == missionId1);
        completedMission.Metrics.ResponseTimeMinutes.Should().Be(30);
        completedMission.Metrics.HandoverTimeMinutes.Should().Be(60);
        completedMission.Metrics.TotalDurationMinutes.Should().Be(100);
        completedMission.TrackingData.TotalCheckpoints.Should().Be(3);
        completedMission.TrackingData.AvgSpeed.Should().Be(50);
        completedMission.TrackingData.MaxSpeed.Should().Be(60);

        var acceptedMission = data.Missions.Single(x => x.MissionId == missionId2);
        acceptedMission.TrackingData.TotalCheckpoints.Should().Be(0);
        acceptedMission.TrackingData.AvgSpeed.Should().Be(0);
        acceptedMission.TrackingData.MaxSpeed.Should().Be(0);
    }

    [Fact]
    public async Task GetAiAccuracyMetricsAsync_ValidData_ReturnsGroupedMetrics()
    {
        // Arrange
        var snake = new Snake
        {
            Id = Guid.NewGuid(),
            CommonName = "Cobra",
            ScientificName = "Naja naja",
            ToxicityLevel = SnakeRiskLevel.Deadly,
            ToxinGroup = ToxinGroup.Neurotoxin
        };
        var blankNameSnake = new Snake
        {
            Id = Guid.NewGuid(),
            CommonName = "   ",
            ScientificName = "Unknownus",
            ToxicityLevel = SnakeRiskLevel.MildlyVenomous,
            ToxinGroup = ToxinGroup.Unknown
        };

        var aiData = new List<AiInferenceEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Incident = new Incident { CurrentAiReviewStatus = AiReviewStatus.ConfirmedCorrect },
                SelectedSnake = snake,
                SelectedConfidence = 0.9
            },
            new()
            {
                Id = Guid.NewGuid(),
                Incident = new Incident { CurrentAiReviewStatus = AiReviewStatus.Corrected },
                SelectedSnake = snake,
                SelectedConfidence = 0.5
            },
            new()
            {
                Id = Guid.NewGuid(),
                Incident = new Incident { CurrentAiReviewStatus = AiReviewStatus.Pending },
                SelectedSnake = snake,
                SelectedConfidence = null
            },
            new()
            {
                Id = Guid.NewGuid(),
                Incident = new Incident { CurrentAiReviewStatus = AiReviewStatus.ConfirmedCorrect },
                SelectedSnake = blankNameSnake,
                SelectedConfidence = 0.8
            }
        };

        _aiRepoMock.Setup(x => x.GetQueryable(false)).Returns(ToAsyncQueryable(aiData));
        _speciesServiceMock.Setup(x => x.GetSupportedSpecies()).Returns(new List<string> { "Naja_naja", "Unknownus" });

        // Act
        var result = await _sut.GetAiAccuracyMetricsAsync(new AnalyticsSpecParams());

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var data = result.Data.Should().BeOfType<List<AiAccuracyDto>>().Subject;
        data.Should().HaveCount(2);

        var cobra = data.Single(x => x.SpeciesName == "Cobra");
        cobra.TotalInferences.Should().Be(3);
        cobra.AccuracyRate.Should().Be(0.5);
        cobra.AverageConfidence.Should().BeApproximately((0.9 + 0.5 + 0.0) / 3.0, 0.0001);

        var unknown = data.Single(x => x.SpeciesName == "Unknown");
        unknown.TotalInferences.Should().Be(1);
        unknown.AccuracyRate.Should().Be(1);
        unknown.AverageConfidence.Should().Be(0.8);
    }

    [Fact]
    public async Task ExportCsvAsync_InvalidExportType_ReturnsValidationWarning()
    {
        // Arrange
        var filter = new AnalyticsSpecParams();

        // Act
        var result = await _sut.ExportCsvAsync("not-supported", filter);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0003);
        result.Data.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // PingHeatmapHotspotAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PingHeatmapHotspotAsync_NoDeviceTokens_ReturnsWarning()
    {
        // Arrange — no users in radius
        _userRepoMock
            .Setup(x => x.GetQueryable(false))
            .Returns(ToAsyncQueryable(new List<User>()));

        var dto = new PingHeatmapHotspotDto { Latitude = 10.7, Longitude = 106.7 };

        // Act
        var result = await _sut.PingHeatmapHotspotAsync(10.7, 106.7);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Analytics_Warning0001);
        result.Data.Should().BeNull();
        _notificationServiceMock.Verify(
            x => x.SendNotificationsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task PingHeatmapHotspotAsync_WithUsers_CallsNotificationServiceAndReturnsSuccess()
    {
        // Arrange — two distinct users with valid location (no DeviceToken required)
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var hotspotLocation = geometryFactory.CreatePoint(new Coordinate(106.7, 10.7));

        var users = new List<User>
        {
            new User 
            { 
                Id = userId1, 
                CurrentLocation = hotspotLocation
            },
            new User 
            { 
                Id = userId2, 
                CurrentLocation = hotspotLocation
            }
        };

        _userRepoMock
            .Setup(x => x.GetQueryable(false))
            .Returns(ToAsyncQueryable(users));

        _notificationServiceMock
            .Setup(x => x.SendNotificationsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, "OK"));

        // Act
        var result = await _sut.PingHeatmapHotspotAsync(10.7, 106.7);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Analytics_Success0001);
        result.Data.Should().NotBeNull();
        _notificationServiceMock.Verify(
            x => x.SendNotificationsAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Count() == 2),
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationType.Alert,
                null),
            Times.Once);
    }

    [Fact]
    public async Task PingHeatmapHotspotAsync_WithCustomMessage_UsesCustomMessageAsBody()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var hotspotLocation = geometryFactory.CreatePoint(new Coordinate(106.7, 10.7));

        var users = new List<User>
        {
            new User 
            { 
                Id = userId, 
                CurrentLocation = hotspotLocation
            }
        };

        var customMsg = "Cảnh báo đặc biệt từ admin!";

        _userRepoMock
            .Setup(x => x.GetQueryable(false))
            .Returns(ToAsyncQueryable(users));

        string? capturedBody = null;
        _notificationServiceMock
            .Setup(x => x.SendNotificationsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>()))
            .Callback<IEnumerable<Guid>, string, string, NotificationType, Guid?>((_, _, body, _, _) => capturedBody = body)
            .ReturnsAsync(new ServiceResult(ResultCodeConst.SYS_Success0002, "OK"));

        // Act
        var result = await _sut.PingHeatmapHotspotAsync(10.7, 106.7, customMsg);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Analytics_Success0001);
        capturedBody.Should().Be(customMsg);
    }

    private static IQueryable<T> ToAsyncQueryable<T>(IEnumerable<T> source)
    {
        return new TestAsyncEnumerable<T>(source);
    }

    private sealed class TestAsyncQueryProvider<TEntity>(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression)
        {
            return new TestAsyncEnumerable<TEntity>(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new TestAsyncEnumerable<TElement>(expression);
        }

        public object? Execute(Expression expression)
        {
            return inner.Execute(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return inner.Execute<TResult>(expression);
        }

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var expectedResultType = typeof(TResult).GetGenericArguments().First();
            var executionResult = typeof(IQueryProvider)
                .GetMethods()
                .First(m => m.Name == nameof(IQueryProvider.Execute) && m.IsGenericMethod)
                .MakeGenericMethod(expectedResultType)
                .Invoke(inner, new[] { expression });

            return (TResult)typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(expectedResultType)
                .Invoke(null, new[] { executionResult })!;
        }
    }

    private sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable)
            : base(enumerable)
        {
        }

        public TestAsyncEnumerable(Expression expression)
            : base(expression)
        {
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
        }

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }

    private sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;

        public ValueTask<bool> MoveNextAsync()
        {
            return new ValueTask<bool>(inner.MoveNext());
        }

        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
