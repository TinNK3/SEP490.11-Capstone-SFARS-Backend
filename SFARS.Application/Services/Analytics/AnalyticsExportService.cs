using SFARS.Application.Common;
using SFARS.Application.Dtos.Analytics;
using SFARS.Application.Utils;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Application.Services.Analytics;

public interface IAnalyticsExportService
{
    Task<IServiceResult> ExportCsvAsync(string exportType, AnalyticsSpecParams filter);
}

public class AnalyticsExportService : IAnalyticsExportService
{
    private readonly IAnalyticsOverviewService _overviewService;
    private readonly IAnalyticsIncidentService _incidentService;
    private readonly IAnalyticsRescuerService _rescuerService;
    private readonly IAnalyticsAiService _aiService;
    private readonly ISystemMessageService _msgService;

    public AnalyticsExportService(
        IAnalyticsOverviewService overviewService,
        IAnalyticsIncidentService incidentService,
        IAnalyticsRescuerService rescuerService,
        IAnalyticsAiService aiService,
        ISystemMessageService msgService)
    {
        _overviewService = overviewService;
        _incidentService = incidentService;
        _rescuerService = rescuerService;
        _aiService = aiService;
        _msgService = msgService;
    }

    public async Task<IServiceResult> ExportCsvAsync(string exportType, AnalyticsSpecParams filter)
    {
        var normalized = (exportType ?? string.Empty).Trim().ToLowerInvariant();

        var handlers = new Dictionary<string, (Func<Task<IServiceResult>> Handler, string FileNameStem)>(StringComparer.OrdinalIgnoreCase)
        {
            ["overview"] = (() => _overviewService.GetOverviewMetricsAsync(filter), "overview"),
            ["incidents"] = (() => _incidentService.GetIncidentTrendsAsync(filter), "incident-trends"),
            ["incidenttrends"] = (() => _incidentService.GetIncidentTrendsAsync(filter), "incident-trends"),
            ["trends"] = (() => _incidentService.GetIncidentTrendsAsync(filter), "incident-trends"),
            ["heatmap"] = (() => _incidentService.GetHeatmapDataAsync(filter), "heatmap"),
            ["rescuers"] = (() => _rescuerService.GetRescuerLeaderboardAsync(filter), "rescuer-leaderboard"),
            ["rescuerleaderboard"] = (() => _rescuerService.GetRescuerLeaderboardAsync(filter), "rescuer-leaderboard"),
            ["leaderboard"] = (() => _rescuerService.GetRescuerLeaderboardAsync(filter), "rescuer-leaderboard"),
            ["ai"] = (() => _aiService.GetAiAccuracyMetricsAsync(filter), "ai-metrics"),
            ["aimetrics"] = (() => _aiService.GetAiAccuracyMetricsAsync(filter), "ai-metrics")
        };

        if (!handlers.TryGetValue(normalized, out var selected))
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0003,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0003));
        }

        var dataResult = await selected.Handler();
        if (!dataResult.ResultCode.Contains(".Success", StringComparison.OrdinalIgnoreCase))
            return dataResult;

        var ictTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, AnalyticsQueryHelper.ResolveIctTimeZone());
        var csv = CsvExporter.ToCsv(dataResult.Data);
        var dto = new ExportResultDto
        {
            FileName = $"analytics-{selected.FileNameStem}-{ictTime:yyyyMMdd-HHmmss}.csv",
            Content = csv,
            ContentType = ExportContentType.Csv,
            GeneratedAtUtc = DateTime.UtcNow
        };

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            dto);
    }
}

