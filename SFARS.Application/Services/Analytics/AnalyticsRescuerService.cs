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

public interface IAnalyticsRescuerService
{
    Task<IServiceResult> GetRescuerLeaderboardAsync(AnalyticsSpecParams filter);
    Task<IServiceResult> GetRescuerMissionHistoryAsync(Guid rescuerId, AnalyticsSpecParams filter);
}

public class AnalyticsRescuerService : IAnalyticsRescuerService
{
    private readonly IGenericRepository<RescueMission, Guid> _rescueRepo;
    private readonly IGenericRepository<User, Guid> _userRepo;
    private readonly ISystemMessageService _msgService;

    public AnalyticsRescuerService(
        IGenericRepository<RescueMission, Guid> rescueRepo,
        IGenericRepository<User, Guid> userRepo,
        ISystemMessageService msgService)
    {
        _rescueRepo = rescueRepo;
        _userRepo = userRepo;
        _msgService = msgService;
    }

    public async Task<IServiceResult> GetRescuerLeaderboardAsync(AnalyticsSpecParams filter)
    {
        var query = AnalyticsQueryHelper.ApplyCreatedAtUtcRange(_rescueRepo.GetQueryable(false), filter);

        var performances = await query
            .Where(x => x.Rescuer != null)
            .GroupBy(x => x.RescuerId)
            .Select(g => new
            {
                RescuerId = g.Key,
                RescuerName = g.FirstOrDefault()!.Rescuer.FullName,
                TotalMissions = g.Count(),
                SuccessCount = g.Count(r => r.Status == RescueStatus.Completed),
                TotalMinutes = g.Where(r => r.StartedAt.HasValue && r.ArrivedAt.HasValue)
                                .Sum(r => EF.Functions.DateDiffMinute(r.StartedAt, r.ArrivedAt) ?? 0),
                MissionsWithTime = g.Count(r => r.StartedAt.HasValue && r.ArrivedAt.HasValue)
            })
            .ToListAsync();

        var dtos = performances.Select(p => new RescuerPerformanceDto
        {
            RescuerId = p.RescuerId,
            RescuerName = p.RescuerName,
            TotalMissions = p.TotalMissions,
            SuccessRate = p.TotalMissions > 0 ? (double)p.SuccessCount / p.TotalMissions : 0,
            AvgResponseTimeMinutes = p.MissionsWithTime > 0 ? (double)p.TotalMinutes / p.MissionsWithTime : 0
        })
        .OrderByDescending(p => p.TotalMissions)
        .ThenByDescending(p => p.SuccessRate)
        .ToList();

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            dtos);
    }

    public async Task<IServiceResult> GetRescuerMissionHistoryAsync(Guid rescuerId, AnalyticsSpecParams filter)
    {
        var rescuer = await _userRepo.GetByIdAsync(rescuerId);
        if (rescuer == null)
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0004,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004),
                null);

        var missionQuery = AnalyticsQueryHelper.ApplyCreatedAtUtcRange(_rescueRepo.GetQueryable(false), filter);
        var missions = await missionQuery
            .Where(x => x.RescuerId == rescuerId)
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.CreatedAt,
                x.StartedAt,
                x.ArrivedAt,
                x.CompletedAt,
                x.RescuerNotes,
                x.PatientConditionAtHandover,
                IncidentCode = x.Incident.Code,
                VictimFullName = x.Incident.Victim.FullName,
                VictimPhone = x.Incident.Victim.Phone,
                VictimAddress = x.Incident.Victim.Address,
                IncidentLocation = x.Incident.Location,
                IncidentPriority = x.Incident.PriorityLevel,
                IncidentDescription = x.Incident.Description,
                Review = x.Review
            })
            .ToListAsync();

        if (missions.Count == 0)
            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                new RescuerMissionHistoryDto
                {
                    RescuerId = rescuerId,
                    RescuerName = rescuer.FullName,
                    TotalMissions = 0,
                    SuccessRate = 0,
                    Missions = new()
                });

        var missionIds = missions.Select(m => m.Id).ToList();
        var trackingMetrics = await _rescueRepo.GetQueryable(false)
            .Where(x => x.RescuerId == rescuerId && missionIds.Contains(x.Id))
            .SelectMany(x => x.TrackingLogs)
            .GroupBy(x => x.MissionId)
            .Select(g => new
            {
                MissionId = g.Key,
                TotalCheckpoints = g.Count(),
                AvgSpeed = g.Where(t => t.SpeedKMH.HasValue).Average(t => t.SpeedKMH!.Value),
                MaxSpeed = g.Where(t => t.SpeedKMH.HasValue).Max(t => (double?)t.SpeedKMH) ?? 0
            })
            .ToDictionaryAsync(x => x.MissionId);

        var missionDtos = missions.Select(m =>
        {
            trackingMetrics.TryGetValue(m.Id, out var tracking);

            return new MissionDetailDto
            {
                MissionId = m.Id,
                IncidentCode = m.IncidentCode,
                VictimName = m.VictimFullName,
                VictimPhone = m.VictimPhone,
                VictimLocation = m.VictimAddress,
                IncidentLocation = new MissionLocationDto
                {
                    Latitude = m.IncidentLocation.Y,
                    Longitude = m.IncidentLocation.X
                },
                Severity = m.IncidentPriority.ToString(),
                Description = m.IncidentDescription,
                Status = m.Status.ToString(),
                Timeline = new MissionTimelineDto
                {
                    MissionAcceptedAt = m.CreatedAt,
                    RescueStartedAt = m.StartedAt,
                    RescueArrivedAt = m.ArrivedAt,
                    RescueCompletedAt = m.CompletedAt
                },
                Metrics = new MissionMetricsDto
                {
                    ResponseTimeMinutes = m.StartedAt.HasValue && m.ArrivedAt.HasValue
                        ? (m.ArrivedAt.Value - m.StartedAt.Value).TotalMinutes
                        : 0,
                    HandoverTimeMinutes = m.ArrivedAt.HasValue && m.CompletedAt.HasValue
                        ? (m.CompletedAt.Value - m.ArrivedAt.Value).TotalMinutes
                        : 0,
                    TotalDurationMinutes = m.CompletedAt.HasValue
                        ? (m.CompletedAt.Value - m.CreatedAt).TotalMinutes
                        : 0
                },
                Details = new MissionDetailsDto
                {
                    RescuerNotes = m.RescuerNotes,
                    PatientConditionAtHandover = m.PatientConditionAtHandover,
                    VictimRating = m.Review?.Rating,
                    VictimComment = m.Review?.Comment
                },
                TrackingData = new TrackingDataDto
                {
                    TotalCheckpoints = tracking?.TotalCheckpoints ?? 0,
                    AvgSpeed = tracking?.AvgSpeed ?? 0,
                    MaxSpeed = tracking?.MaxSpeed ?? 0,
                    TrackingPoints = new()
                }
            };
        })
        .OrderByDescending(x => x.Timeline.MissionAcceptedAt)
        .ToList();

        var completedMissions = missionDtos.Count(x => x.Status == RescueStatus.Completed.ToString());
        var successRate = missionDtos.Count > 0 ? (double)completedMissions / missionDtos.Count : 0;

        var result = new RescuerMissionHistoryDto
        {
            RescuerId = rescuerId,
            RescuerName = rescuer.FullName,
            TotalMissions = missionDtos.Count,
            SuccessRate = Math.Round(successRate, 2),
            Missions = missionDtos
        };

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            result);
    }
}

