using Microsoft.EntityFrameworkCore;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Analytics;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Application.Services.Analytics;

public interface IAnalyticsIncidentService
{
    Task<IServiceResult> GetIncidentTrendsAsync(AnalyticsSpecParams filter);
    Task<IServiceResult> GetHeatmapDataAsync(AnalyticsSpecParams filter);
}

public class AnalyticsIncidentService : IAnalyticsIncidentService
{
    private readonly IGenericRepository<Incident, Guid> _incidentRepo;
    private readonly IGenericRepository<RescueMission, Guid> _rescueRepo;
    private readonly IGenericRepository<AiInferenceReview, Guid> _aiReviewRepo;
    private readonly ISystemMessageService _msgService;

    public AnalyticsIncidentService(
        IGenericRepository<Incident, Guid> incidentRepo,
        IGenericRepository<RescueMission, Guid> rescueRepo,
        IGenericRepository<AiInferenceReview, Guid> aiReviewRepo,
        ISystemMessageService msgService)
    {
        _incidentRepo = incidentRepo;
        _rescueRepo = rescueRepo;
        _aiReviewRepo = aiReviewRepo;
        _msgService = msgService;
    }

    public async Task<IServiceResult> GetIncidentTrendsAsync(AnalyticsSpecParams filter)
    {
        var query = AnalyticsQueryHelper.ApplyCreatedAtUtcRange(_incidentRepo.GetQueryable(false), filter);
        const int ictOffsetHours = 7;

        var incidentData = await query
            .Select(x => new
            {
                LocalDate = x.CreatedAt.AddHours(ictOffsetHours).Date,
                x.CurrentStatus,
                x.PriorityLevel
            })
            .ToListAsync();

        var missionData = await AnalyticsQueryHelper.ApplyCreatedAtUtcRange(_rescueRepo.GetQueryable(false), filter)
            .Where(x => x.Status == RescueStatus.Completed && x.StartedAt.HasValue && x.ArrivedAt.HasValue)
            .Select(x => new
            {
                LocalDate = x.Incident.CreatedAt.AddHours(ictOffsetHours).Date,
                ResponseMinutes = EF.Functions.DateDiffMinute(x.StartedAt, x.ArrivedAt) ?? 0
            })
            .ToListAsync();

        var aiReviewData = await AnalyticsQueryHelper.ApplyCreatedAtUtcRange(_aiReviewRepo.GetQueryable(false), filter)
            .Select(x => new
            {
                LocalDate = x.Incident.CreatedAt.AddHours(ictOffsetHours).Date,
                x.ReviewStatus
            })
            .ToListAsync();

        var missionAvgByDate = missionData
            .GroupBy(x => x.LocalDate)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ResponseMinutes).DefaultIfEmpty(0).Average());

        var aiByDate = aiReviewData
            .GroupBy(x => x.LocalDate)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ReviewStatus).ToList());

        var trends = incidentData
            .GroupBy(x => x.LocalDate)
            .Select(g => new IncidentTrendDto
            {
                Date = g.Key,
                TotalIncidents = g.Count(),
                CriticalCount = g.Count(x => x.PriorityLevel == SeverityLevel.Critical),
                ResolvedPercent = g.Count() > 0
                    ? (double)g.Count(x => x.CurrentStatus == IncidentStatus.Closed) / g.Count() * 100
                    : 0,
                AvgResponseTimeMinutes = missionAvgByDate.GetValueOrDefault(g.Key, 0),
                AiAccuracy = CalculateAiAccuracy(aiByDate.GetValueOrDefault(g.Key, new List<AiReviewStatus>()))
            })
            .OrderBy(x => x.Date)
            .ToList();

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            trends);
    }

    public async Task<IServiceResult> GetHeatmapDataAsync(AnalyticsSpecParams filter)
    {
        var query = AnalyticsQueryHelper.ApplyCreatedAtUtcRange(_incidentRepo.GetQueryable(false), filter);
        query = ApplyBoundingBox(query, filter);

        var rawData = await query
            .Where(x => x.Location != null)
            .Select(x => new
            {
                x.Location,
                x.PriorityLevel,
                x.CurrentStatus
            })
            .ToListAsync();

        if (rawData.Count == 0)
            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                Enumerable.Empty<HeatmapDataDto>());

        var missionQuery = AnalyticsQueryHelper.ApplyCreatedAtUtcRange(_rescueRepo.GetQueryable(false), filter);
        missionQuery = ApplyMissionBoundingBox(missionQuery, filter);

        var missionData = await missionQuery
            .Where(x => x.Status == RescueStatus.Completed && x.StartedAt.HasValue && x.ArrivedAt.HasValue)
            .Select(x => new
            {
                Lat = x.Incident.Location.Y,
                Lng = x.Incident.Location.X,
                ResponseMinutes = EF.Functions.DateDiffMinute(x.StartedAt, x.ArrivedAt) ?? 0
            })
            .ToListAsync();

        var raw = rawData.Select(x => new
        {
            Lat = x.Location.Y,
            Lng = x.Location.X,
            Weight = (int)x.PriorityLevel,
            x.PriorityLevel,
            x.CurrentStatus
        }).ToList();

        var zoom = filter.Zoom ?? 8;
        var gridDeg = zoom switch
        {
            <= 5 => 1.0,
            <= 7 => 0.5,
            <= 9 => 0.25,
            <= 11 => 0.1,
            <= 13 => 0.01,
            <= 15 => 0.005,
            _ => 0.001
        };

        const int maxClusters = 2000;

        var clusters = raw
            .GroupBy(p => (
                CellLat: Math.Floor(p.Lat / gridDeg) * gridDeg + gridDeg / 2,
                CellLng: Math.Floor(p.Lng / gridDeg) * gridDeg + gridDeg / 2
            ))
            .Select(g => new HeatmapDataDto
            {
                Latitude = g.Key.CellLat,
                Longitude = g.Key.CellLng,
                Weight = g.Sum(p => p.Weight),
                IncidentCount = g.Count(),
                CriticalCount = g.Count(x => x.PriorityLevel == SeverityLevel.Critical),
                SeverityBreakdown = new SeverityBreakdownDto
                {
                    Critical = g.Count(x => x.PriorityLevel == SeverityLevel.Critical),
                    High = g.Count(x => x.PriorityLevel == SeverityLevel.High),
                    Medium = g.Count(x => x.PriorityLevel == SeverityLevel.Medium),
                    Low = g.Count(x => x.PriorityLevel == SeverityLevel.Low)
                },
                ResolvedPercent = g.Count() > 0
                    ? (double)g.Count(x => x.CurrentStatus == IncidentStatus.Closed) / g.Count() * 100
                    : 0,
                AvgResponseTimeMinutes = missionData
                    .Where(m =>
                        m.Lat >= g.Key.CellLat - gridDeg / 2 &&
                        m.Lat < g.Key.CellLat + gridDeg / 2 &&
                        m.Lng >= g.Key.CellLng - gridDeg / 2 &&
                        m.Lng < g.Key.CellLng + gridDeg / 2)
                    .Select(m => m.ResponseMinutes)
                    .DefaultIfEmpty(0)
                    .Average()
            })
            .OrderByDescending(c => c.Weight)
            .Take(maxClusters)
            .ToList();

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            clusters);
    }

    private static IQueryable<Incident> ApplyBoundingBox(IQueryable<Incident> query, AnalyticsSpecParams filter)
    {
        if (filter.MinLat.HasValue) query = query.Where(x => x.Location.Y >= filter.MinLat.Value);
        if (filter.MaxLat.HasValue) query = query.Where(x => x.Location.Y <= filter.MaxLat.Value);
        if (filter.MinLng.HasValue) query = query.Where(x => x.Location.X >= filter.MinLng.Value);
        if (filter.MaxLng.HasValue) query = query.Where(x => x.Location.X <= filter.MaxLng.Value);
        return query;
    }

    private static IQueryable<RescueMission> ApplyMissionBoundingBox(IQueryable<RescueMission> query, AnalyticsSpecParams filter)
    {
        if (filter.MinLat.HasValue) query = query.Where(x => x.Incident.Location.Y >= filter.MinLat.Value);
        if (filter.MaxLat.HasValue) query = query.Where(x => x.Incident.Location.Y <= filter.MaxLat.Value);
        if (filter.MinLng.HasValue) query = query.Where(x => x.Incident.Location.X >= filter.MinLng.Value);
        if (filter.MaxLng.HasValue) query = query.Where(x => x.Incident.Location.X <= filter.MaxLng.Value);
        return query;
    }

    private static double CalculateAiAccuracy(List<AiReviewStatus> aiReviews)
    {
        if (aiReviews.Count == 0) return 0;

        var reviewedCount = aiReviews.Count(r =>
            r != AiReviewStatus.Pending &&
            r != AiReviewStatus.UnableToAssess);

        if (reviewedCount == 0) return 0;

        var confirmedCorrect = aiReviews.Count(r => r == AiReviewStatus.ConfirmedCorrect);
        return Math.Round((double)confirmedCorrect / reviewedCount * 100, 2);
    }
}

