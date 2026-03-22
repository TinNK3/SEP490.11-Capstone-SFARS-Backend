using SFARS.Application.Services.Analytics;
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

    public AnalyticsService(
        IAnalyticsOverviewService overviewService,
        IAnalyticsIncidentService incidentService,
        IAnalyticsRescuerService rescuerService,
        IAnalyticsAiService aiService,
        IAnalyticsExportService exportService)
    {
        _overviewService = overviewService;
        _incidentService = incidentService;
        _rescuerService = rescuerService;
        _aiService = aiService;
        _exportService = exportService;
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
}

