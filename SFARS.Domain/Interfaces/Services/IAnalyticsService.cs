using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SFARS.Domain.Interfaces.Services;

public interface IAnalyticsService
{
    Task<IServiceResult> GetOverviewMetricsAsync(AnalyticsSpecParams filter);
    Task<IServiceResult> GetIncidentTrendsAsync(AnalyticsSpecParams filter);
    Task<IServiceResult> GetHeatmapDataAsync(AnalyticsSpecParams filter);
    Task<IServiceResult> GetRescuerLeaderboardAsync(AnalyticsSpecParams filter);
    Task<IServiceResult> GetRescuerMissionHistoryAsync(Guid rescuerId, AnalyticsSpecParams filter);
    Task<IServiceResult> GetAiAccuracyMetricsAsync(AnalyticsSpecParams filter);
    Task<IServiceResult> GetSnakeIncidentTrackingAsync(Guid snakeId, AnalyticsSpecParams filter);
    Task<IServiceResult> ExportCsvAsync(string exportType, AnalyticsSpecParams filter);
    Task<IServiceResult> PingHeatmapHotspotAsync(double latitude, double longitude, string? customMessage = null);
}
