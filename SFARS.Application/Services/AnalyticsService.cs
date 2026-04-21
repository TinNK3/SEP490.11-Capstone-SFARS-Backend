using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using SFARS.Application.Common;
using SFARS.Application.Interfaces.Services;
using SFARS.Application.Services.Analytics;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Application.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly IAnalyticsOverviewService _overviewService;
    private readonly IAnalyticsIncidentService _incidentService;
    private readonly IAnalyticsRescuerService _rescuerService;
    private readonly IAnalyticsAiService _aiService;
    private readonly IAnalyticsExportService _exportService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemMessageService _msgService;

    public AnalyticsService(
        IAnalyticsOverviewService overviewService,
        IAnalyticsIncidentService incidentService,
        IAnalyticsRescuerService rescuerService,
        IAnalyticsAiService aiService,
        IAnalyticsExportService exportService,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        ISystemMessageService msgService)
    {
        _overviewService = overviewService;
        _incidentService = incidentService;
        _rescuerService = rescuerService;
        _aiService = aiService;
        _exportService = exportService;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _msgService = msgService;
    }

    public Task<IServiceResult> GetOverviewMetricsAsync(AnalyticsSpecParams filter)
        => _overviewService.GetOverviewMetricsAsync(filter);

    public Task<IServiceResult> GetIncidentTrendsAsync(AnalyticsSpecParams filter)
        => _incidentService.GetIncidentTrendsAsync(filter);

    public Task<IServiceResult> GetHeatmapDataAsync(AnalyticsSpecParams filter)
        => _incidentService.GetHeatmapDataAsync(filter);

    public Task<IServiceResult> GetRescuerLeaderboardAsync(AnalyticsSpecParams filter)
        => _rescuerService.GetRescuerLeaderboardAsync(filter);

    public Task<IServiceResult> GetRescuerMissionHistoryAsync(Guid rescuerId, AnalyticsSpecParams filter)
        => _rescuerService.GetRescuerMissionHistoryAsync(rescuerId, filter);

    public Task<IServiceResult> GetAiAccuracyMetricsAsync(AnalyticsSpecParams filter)
        => _aiService.GetAiAccuracyMetricsAsync(filter);

    public Task<IServiceResult> GetSnakeIncidentTrackingAsync(Guid snakeId, AnalyticsSpecParams filter)
        => _aiService.GetSnakeIncidentTrackingAsync(snakeId, filter);

    public Task<IServiceResult> ExportCsvAsync(string exportType, AnalyticsSpecParams filter)
        => _exportService.ExportCsvAsync(exportType, filter);

    public async Task<IServiceResult> PingHeatmapHotspotAsync(double latitude, double longitude, string? customMessage = null)
    {
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var hotspotLocation = geometryFactory.CreatePoint(new Coordinate(longitude, latitude));
        
        double radiusInMeters = 10000;

        var userIds = await _unitOfWork.Repository<User, Guid>().GetQueryable(false)
            .Where(u => u.CurrentLocation != null 
                     && u.CurrentLocation.Distance(hotspotLocation) <= radiusInMeters)
            .Select(u => u.Id)
            .ToListAsync();

        if (userIds.Count == 0)
        {
            return new ServiceResult(
                ResultCodeConst.Analytics_Warning0001,
                await _msgService.GetMessageAsync(ResultCodeConst.Analytics_Warning0001));
        }

        var title = "⚠️ Cảnh báo khu vực nguy hiểm";
        var body = !string.IsNullOrWhiteSpace(customMessage)
            ? customMessage
            : $"Khu vực gần ({latitude:F4}°N, {longitude:F4}°E) được ghi nhận có nhiều sự cố rắn cắn. Hãy đề cao cảnh giác khi di chuyển trong khu vực này.";

        await _notificationService.SendNotificationsAsync(
            userIds, title, body, NotificationType.Alert);

        return new ServiceResult(
            ResultCodeConst.Analytics_Success0001,
            await _msgService.GetMessageAsync(ResultCodeConst.Analytics_Success0001),
            new { PingedUsers = userIds.Count, Latitude = latitude, Longitude = longitude });
    }
}