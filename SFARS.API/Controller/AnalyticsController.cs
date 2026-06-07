using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Admin;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Params;

namespace SFARS.API.Controller
{
    [ApiController]
    [Authorize(Roles = UserTypeConstants.Admin)]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public AnalyticsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        [HttpGet(APIRoute.Analytics.Overview, Name = nameof(GetOverviewAsync))]
        public async Task<IActionResult> GetOverviewAsync([FromQuery] AnalyticsSpecParams filter)
        {
            var result = await _analyticsService.GetOverviewMetricsAsync(filter);
            return this.ToIActionResult(result);
        }

        [HttpGet(APIRoute.Analytics.Incidents, Name = nameof(GetIncidentTrendsAsync))]
        public async Task<IActionResult> GetIncidentTrendsAsync([FromQuery] AnalyticsSpecParams filter)
        {
            var result = await _analyticsService.GetIncidentTrendsAsync(filter);
            return this.ToIActionResult(result);
        }

        [AllowAnonymous]
        [HttpGet(APIRoute.Analytics.Heatmap, Name = nameof(GetHeatmapAsync))]
        public async Task<IActionResult> GetHeatmapAsync([FromQuery] AnalyticsSpecParams filter)
        {
            var result = await _analyticsService.GetHeatmapDataAsync(filter);
            return this.ToIActionResult(result);
        }

        [HttpGet(APIRoute.Analytics.Rescuers, Name = nameof(GetRescuerLeaderboardAsync))]
        public async Task<IActionResult> GetRescuerLeaderboardAsync([FromQuery] AnalyticsSpecParams filter)
        {
            var result = await _analyticsService.GetRescuerLeaderboardAsync(filter);
            return this.ToIActionResult(result);
        }

        [HttpGet(APIRoute.Analytics.RescuerMissionHistory, Name = nameof(GetRescuerMissionHistoryAsync))]
        public async Task<IActionResult> GetRescuerMissionHistoryAsync([FromQuery] AnalyticsSpecParams filter)
        {
            var result = await _analyticsService.GetRescuerMissionHistoryAsync(filter);
            return this.ToIActionResult(result);
        }

        [HttpGet(APIRoute.Analytics.Ai, Name = nameof(GetAiMetricsAsync))]
        public async Task<IActionResult> GetAiMetricsAsync([FromQuery] AnalyticsSpecParams filter)
        {
            var result = await _analyticsService.GetAiAccuracyMetricsAsync(filter);
            return this.ToIActionResult(result);
        }

        [HttpGet(APIRoute.Analytics.SnakeIncidentTracking, Name = nameof(GetSnakeIncidentTrackingAsync))]
        public async Task<IActionResult> GetSnakeIncidentTrackingAsync([FromRoute] Guid snakeId, [FromQuery] AnalyticsSpecParams filter)
        {
            var result = await _analyticsService.GetSnakeIncidentTrackingAsync(snakeId, filter);
            return this.ToIActionResult(result);
        }

        [HttpGet(APIRoute.Analytics.Export, Name = nameof(ExportAsync))]
        public async Task<IActionResult> ExportAsync([FromQuery] string exportType, [FromQuery] AnalyticsSpecParams filter)
        {
            var result = await _analyticsService.ExportCsvAsync(exportType, filter);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [POST] Admin pings a high-risk heatmap hotspot — broadcasts an FCM Alert push notification
        /// to all community users with registered device tokens.
        /// </summary>
        [HttpPost(APIRoute.Analytics.PingHeatmapHotspot, Name = nameof(PingHeatmapHotspotAsync))]
        public async Task<IActionResult> PingHeatmapHotspotAsync([FromBody] PingHeatmapHotspotRequest req)
        {
            var result = await _analyticsService.PingHeatmapHotspotAsync(req.Latitude, req.Longitude, req.CustomMessage);
            return this.ToIActionResult(result);
        }
    }
}
