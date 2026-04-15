using FluentAssertions;
using MapsterMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Incident;
using SFARS.Application.Dtos.AiInference;
using SFARS.Application.Dtos.AiReview;
using SFARS.Application.Services;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Repositories.Base;
using NetTopologySuite.Geometries;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.Interfaces;
using SFARS.Infrastructure.Configurations;
using SFARS.Infrastructure.Hubs;
using System.Linq.Expressions;

namespace SFARS.Tests.Application.Services.Incidents;

public class IncidentServiceTests
{
    protected readonly Mock<ISystemMessageService> _msgServiceMock;
    protected readonly Mock<IUnitOfWork> _unitOfWorkMock;
    protected readonly Mock<IMapper> _mapperMock;
    protected readonly Mock<ILogger<IncidentService>> _loggerMock;
    protected readonly Mock<IFileStorageService> _fileStorageServiceMock;
    protected readonly Mock<IOptions<StorageOptions>> _storageOptionsMock;
    protected readonly Mock<ISosSpamGuardService> _spamGuardMock;
    protected readonly Mock<IAiReviewService<SubmitAiReviewRequestDto, FirstAidStepDto>> _aiReviewServiceMock;
    protected readonly Mock<IGenericRepository<Incident, Guid>> _incidentRepoMock;
    protected readonly Mock<IGenericRepository<IncidentMedia, Guid>> _incidentMediaRepoMock;
    protected readonly Mock<IGenericRepository<IncidentStatusHistory, Guid>> _statusHistoryRepoMock;
    protected readonly Mock<IGenericRepository<IncidentChat, Guid>> _chatRepoMock;
    protected readonly Mock<IGenericRepository<NotificationLog, Guid>> _notificationRepoMock;
    protected readonly Mock<IGenericRepository<User, Guid>> _userRepoMock;
    protected readonly Mock<ISpeechToTextService> _sttServiceMock;
    protected readonly Mock<IHubContext<RescueDispatchHub>> _rescueHubMock;
    protected readonly Mock<IHubContext<LocationTrackingHub>> _locationHubMock;
    protected readonly Mock<IFcmPushService> _fcmServiceMock;
    protected readonly Mock<IConfiguration> _configurationMock;
    protected readonly Mock<IDispatchService> _dispatchServiceMock;
    protected readonly IncidentService _sut; // System Under Test

    public IncidentServiceTests()
    {
        _msgServiceMock = new Mock<ISystemMessageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mapperMock = new Mock<IMapper>();
        _loggerMock = new Mock<ILogger<IncidentService>>();
        _fileStorageServiceMock = new Mock<IFileStorageService>();
        _storageOptionsMock = new Mock<IOptions<StorageOptions>>();
        _spamGuardMock = new Mock<ISosSpamGuardService>();
        _aiReviewServiceMock = new Mock<IAiReviewService<SubmitAiReviewRequestDto, FirstAidStepDto>>();
        _incidentRepoMock = new Mock<IGenericRepository<Incident, Guid>>();
        _incidentMediaRepoMock = new Mock<IGenericRepository<IncidentMedia, Guid>>();
        _statusHistoryRepoMock = new Mock<IGenericRepository<IncidentStatusHistory, Guid>>();
        _chatRepoMock = new Mock<IGenericRepository<IncidentChat, Guid>>();
        _notificationRepoMock = new Mock<IGenericRepository<NotificationLog, Guid>>();
        _userRepoMock = new Mock<IGenericRepository<User, Guid>>();
        _sttServiceMock = new Mock<ISpeechToTextService>();
        _rescueHubMock = new Mock<IHubContext<RescueDispatchHub>>();
        _locationHubMock = new Mock<IHubContext<LocationTrackingHub>>();
        _fcmServiceMock = new Mock<IFcmPushService>();
        _configurationMock = new Mock<IConfiguration>();
        _dispatchServiceMock = new Mock<IDispatchService>();

        // Setup repositories
        _unitOfWorkMock.Setup(x => x.Repository<Incident, Guid>()).Returns(_incidentRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<IncidentMedia, Guid>()).Returns(_incidentMediaRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<IncidentStatusHistory, Guid>()).Returns(_statusHistoryRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<IncidentChat, Guid>()).Returns(_chatRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<NotificationLog, Guid>()).Returns(_notificationRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<User, Guid>()).Returns(_userRepoMock.Object);

        // Setup default message service
        _msgServiceMock.Setup(x => x.GetMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((string msgId) => $"Message for {msgId}");

        // Setup StorageOptions
        var storageOptions = new StorageOptions
        {
            MaxUploadBytes = 10 * 1024 * 1024, // 10MB
            IncidentMediaFolderFormat = "incidents/{0}/media",
            AllowedImageTypes = new[] { "image/jpeg", "image/jpg", "image/png" },
            AllowedVideoTypes = new[] { "video/mp4", "video/quicktime" }
        };
        _storageOptionsMock.Setup(x => x.Value).Returns(storageOptions);

        _sut = new IncidentService(
            _msgServiceMock.Object,
            _unitOfWorkMock.Object,
            _mapperMock.Object,
            _loggerMock.Object,
            _fileStorageServiceMock.Object,
            _storageOptionsMock.Object,
            _spamGuardMock.Object,
            _aiReviewServiceMock.Object,
            _sttServiceMock.Object,
            _rescueHubMock.Object,
            _locationHubMock.Object,
            _fcmServiceMock.Object,
            _configurationMock.Object,
            _dispatchServiceMock.Object
        );
    }

    #region CreateIncidentAsync Tests

    [Fact]
    public async Task CreateIncidentAsync_EmptyUserId_ReturnsAuthWarning()
    {
        // Arrange
        var dto = CreateValidIncidentDto();

        // Act
        var result = await _sut.CreateIncidentAsync(Guid.Empty, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0013);
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task CreateIncidentAsync_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = CreateValidIncidentDto();

        _userRepoMock.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.CreateIncidentAsync(userId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
    }

    [Fact]
    public async Task CreateIncidentAsync_ValidRequest_CreatesAllEntities()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = CreateValidIncidentDto();
        var user = CreateValidUser(userId);

        _userRepoMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _unitOfWorkMock.Setup(x => x.GetNextSequenceValueAsync(SequenceNames.IncidentCode)).ReturnsAsync(1L);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(4); // 4 entities saved

        _mapperMock.Setup(x => x.Map<IncidentDto>(It.IsAny<Incident>()))
            .Returns((Incident i) => new IncidentDto
            {
                Id = i.Id,
                Code = i.Code,
                CurrentStatus = i.CurrentStatus
            });

        // Act
        var result = await _sut.CreateIncidentAsync(userId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Incident_Success0001);
        result.Data.Should().NotBeNull();
        result.Data.Should().BeOfType<IncidentDto>();

        // Verify all entities were added
        _incidentRepoMock.Verify(x => x.AddAsync(It.IsAny<Incident>()), Times.Once);
        _statusHistoryRepoMock.Verify(x => x.AddAsync(It.IsAny<IncidentStatusHistory>()), Times.Once);
        _chatRepoMock.Verify(x => x.AddAsync(It.IsAny<IncidentChat>()), Times.Once);
        _notificationRepoMock.Verify(x => x.AddAsync(It.IsAny<NotificationLog>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateIncidentAsync_ValidRequest_GeneratesCorrectCodeFormat()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = CreateValidIncidentDto();
        var user = CreateValidUser(userId);
        var sequenceValue = 42L;

        _userRepoMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _unitOfWorkMock.Setup(x => x.GetNextSequenceValueAsync(SequenceNames.IncidentCode)).ReturnsAsync(sequenceValue);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(4);

        Incident? capturedIncident = null;
        _incidentRepoMock.Setup(x => x.AddAsync(It.IsAny<Incident>()))
            .Callback<Incident>(i => capturedIncident = i)
            .Returns(Task.CompletedTask);

        _mapperMock.Setup(x => x.Map<IncidentDto>(It.IsAny<Incident>()))
            .Returns((Incident i) => new IncidentDto { Id = i.Id, Code = i.Code });

        // Act
        var result = await _sut.CreateIncidentAsync(userId, dto);

        // Assert
        capturedIncident.Should().NotBeNull();
        capturedIncident!.Code.Should().MatchRegex(@"^SOS-\d{4}-00042$");
    }

    [Fact]
    public async Task CreateIncidentAsync_SaveFails_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = CreateValidIncidentDto();
        var user = CreateValidUser(userId);

        _userRepoMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _unitOfWorkMock.Setup(x => x.GetNextSequenceValueAsync(SequenceNames.IncidentCode)).ReturnsAsync(1L);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(0); // Save failed

        // Act
        var result = await _sut.CreateIncidentAsync(userId, dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Fail0001);
    }

    #endregion

    #region GetMyIncidentsAsync Tests

    [Fact]
    public async Task GetMyIncidentsAsync_EmptyUserId_ReturnsAuthWarning()
    {
        // Act
        var result = await _sut.GetMyIncidentsAsync(Guid.Empty, new SFARS.Domain.Specifications.Params.IncidentSpecParams());

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0013);
    }

    [Fact]
    public async Task GetMyIncidentsAsync_ValidUser_ReturnsIncidents()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidents = new List<IncidentHistoryDto>
        {
            new() { Id = Guid.NewGuid(), Code = "SOS-2026-00001" },
            new() { Id = Guid.NewGuid(), Code = "SOS-2026-00002" }
        };

        _incidentRepoMock.Setup(x => x.CountAsync(It.IsAny<ISpecification<Incident>>()))
            .ReturnsAsync(incidents.Count);

        _incidentRepoMock.Setup(x => x.GetAllWithSpecAndSelectorAsync(
                It.IsAny<ISpecification<Incident>>(),
                It.IsAny<Expression<Func<Incident, IncidentHistoryDto>>>(),
                It.IsAny<bool>()))
            .ReturnsAsync(incidents);

        // Act
        var result = await _sut.GetMyIncidentsAsync(userId, new SFARS.Domain.Specifications.Params.IncidentSpecParams());

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var pagedResult = result.Data as SFARS.Application.Dtos.PaginatedResultDto<IncidentHistoryDto>;
        pagedResult!.Items.Should().BeEquivalentTo(incidents);
    }

    [Fact]
    public async Task GetMyIncidentsAsync_NoIncidents_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var emptyList = new List<IncidentHistoryDto>();

        _incidentRepoMock.Setup(x => x.CountAsync(It.IsAny<ISpecification<Incident>>()))
            .ReturnsAsync(0);

        _incidentRepoMock.Setup(x => x.GetAllWithSpecAndSelectorAsync(
                It.IsAny<ISpecification<Incident>>(),
                It.IsAny<Expression<Func<Incident, IncidentHistoryDto>>>(),
                It.IsAny<bool>()))
            .ReturnsAsync(emptyList);

        // Act
        var result = await _sut.GetMyIncidentsAsync(userId, new SFARS.Domain.Specifications.Params.IncidentSpecParams());

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var pagedResult = result.Data as SFARS.Application.Dtos.PaginatedResultDto<IncidentHistoryDto>;
        pagedResult!.Items.Should().BeEquivalentTo(emptyList);
    }

    [Fact]
    public async Task GetMyIncidentsAsync_WithPagination_AppliesPagingCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var page = 2;
        var pageSize = 5;

        _incidentRepoMock.Setup(x => x.CountAsync(It.IsAny<ISpecification<Incident>>()))
            .ReturnsAsync(10);

        BaseSpecification<Incident>? capturedSpec = null;
        _incidentRepoMock.Setup(x => x.GetAllWithSpecAndSelectorAsync(
                It.IsAny<ISpecification<Incident>>(),
                It.IsAny<Expression<Func<Incident, IncidentHistoryDto>>>(),
                It.IsAny<bool>()))
            .Callback<ISpecification<Incident>, Expression<Func<Incident, IncidentHistoryDto>>, bool>(
                (spec, _, _) => capturedSpec = spec as BaseSpecification<Incident>)
            .ReturnsAsync(new List<IncidentHistoryDto>());

        // Act
        await _sut.GetMyIncidentsAsync(userId, new SFARS.Domain.Specifications.Params.IncidentSpecParams { Page = page, PageSize = pageSize });

        // Assert
        capturedSpec.Should().NotBeNull();
        capturedSpec!.Skip.Should().Be((page - 1) * pageSize);
        capturedSpec.Take.Should().Be(pageSize);
    }

    #endregion

    #region GetIncidentByIdAsync Tests



    [Fact]
    public async Task GetIncidentByIdAsync_IncidentNotFound_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();

        _incidentRepoMock.Setup(x => x.GetWithSpecAsync(
                It.IsAny<ISpecification<Incident>>(),
                It.IsAny<bool>()))
            .ReturnsAsync((Incident?)null);

        // Act
        var result = await _sut.GetIncidentByIdAsync(incidentId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
    }



    [Fact]
    public async Task GetIncidentByIdAsync_AsVictim_ReturnsIncident()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        var incident = new Incident
        {
            Id = incidentId,
            Code = "SOS-2026-00001",
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending,
            Location = new Point(106.660172, 10.762622) { SRID = 4326 }
        };

        _incidentRepoMock.Setup(x => x.GetWithSpecAsync(
                It.IsAny<ISpecification<Incident>>(),
                It.IsAny<bool>()))
            .ReturnsAsync(incident);

        _aiReviewServiceMock.Setup(x => x.GetEffectiveFirstAidProtocolAsync(incident))
            .Returns(Task.FromResult<(List<FirstAidStepDto>, List<string>)>((new List<FirstAidStepDto>(), new List<string>())));

        // Act
        // Act
        var result = await _sut.GetIncidentByIdAsync(incidentId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var dto = result.Data as IncidentDetailDto;
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(incidentId);
        dto.Code.Should().Be("SOS-2026-00001");
    }

    [Fact]
    public async Task GetIncidentByIdAsync_ValidRequest_ReturnsCorrectDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        var expectedTime = DateTime.UtcNow;
        var incident = new Incident
        {
            Id = incidentId,
            Code = "SOS-2026-00001",
            AddressString = "123 Test Street",
            Description = "Test incident",
            CurrentStatus = IncidentStatus.Assigned,
            PriorityLevel = SeverityLevel.High,
            VictimId = userId,
            Victim = new User { Id = userId, FirstName = "Test", LastName = "User" },
            CreatedAt = expectedTime,
            Location = new Point(106.660172, 10.762622) { SRID = 4326 }
        };

        _incidentRepoMock.Setup(x => x.GetWithSpecAsync(
                It.IsAny<ISpecification<Incident>>(),
                It.IsAny<bool>()))
            .ReturnsAsync(incident);

        _aiReviewServiceMock.Setup(x => x.GetEffectiveFirstAidProtocolAsync(incident))
            .Returns(Task.FromResult<(List<FirstAidStepDto>, List<string>)>((new List<FirstAidStepDto>(), new List<string>())));

        // Act
        // Act
        var result = await _sut.GetIncidentByIdAsync(incidentId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var dto = result.Data as IncidentDetailDto;
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(incidentId);
        dto.Code.Should().Be("SOS-2026-00001");
        dto.Latitude.Should().Be(10.762622);
        dto.Longitude.Should().Be(106.660172);
        dto.CreatedAt.Should().Be(expectedTime);
    }

    #endregion

    #region Helper Methods

    private static IncidentDto CreateValidIncidentDto()
    {
        return new IncidentDto
        {
            Latitude = 10.762622,
            Longitude = 106.660172,
            AddressString = "123 Test Street, District 1, Ho Chi Minh City",
            Description = "Snake bite emergency - venomous snake suspected",
            PriorityLevel = SeverityLevel.High
        };
    }

    private static User CreateValidUser(Guid userId)
    {
        return new User
        {
            Id = userId,
            Email = "victim@test.com",
            FirstName = "Test",
            LastName = "Victim",
            Status = UserStatus.Active,
            PasswordHash = "hashed"
        };
    }

    #endregion
}