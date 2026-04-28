using Microsoft.EntityFrameworkCore;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Analytics;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Application.Services.Analytics;

public interface IAnalyticsAiService
{
    Task<IServiceResult> GetAiAccuracyMetricsAsync(AnalyticsSpecParams filter);
    Task<IServiceResult> GetSnakeIncidentTrackingAsync(Guid snakeId, AnalyticsSpecParams filter);
}

public class AnalyticsAiService : IAnalyticsAiService
{
    private readonly IGenericRepository<AiInference, Guid> _aiRepo;
    private readonly IGenericRepository<AiInferenceReview, Guid> _aiReviewRepo;
    private readonly IGenericRepository<RescueMission, Guid> _rescueRepo;
    private readonly ISpeciesClassificationService _speciesService;
    private readonly ISystemMessageService _msgService;

    public AnalyticsAiService(
        IGenericRepository<AiInference, Guid> aiRepo,
        IGenericRepository<AiInferenceReview, Guid> aiReviewRepo,
        IGenericRepository<RescueMission, Guid> rescueRepo,
        ISpeciesClassificationService speciesService,
        ISystemMessageService msgService)
    {
        _aiRepo = aiRepo;
        _aiReviewRepo = aiReviewRepo;
        _rescueRepo = rescueRepo;
        _speciesService = speciesService;
        _msgService = msgService;
    }

    public async Task<IServiceResult> GetAiAccuracyMetricsAsync(AnalyticsSpecParams filter)
    {
        var supportedScientificNames = _speciesService.GetSupportedSpecies()
            .Select(x => x.Replace("_", " "))
            .ToList();

        var query = AnalyticsQueryHelper.ApplyCreatedAtUtcRange(_aiRepo.GetQueryable(false), filter);

        var metrics = await query
            .Where(x => x.SelectedSnake != null && supportedScientificNames.Contains(x.SelectedSnake.ScientificName))
            .Select(x => new
            {
                SpeciesName = x.SelectedSnake!.CommonName,
                x.SelectedConfidence,
                ReviewStatus = x.Incident.CurrentAiReviewStatus
            })
            .GroupBy(x => x.SpeciesName)
            .Select(g => new
            {
                SpeciesName = g.Key,
                TotalInferences = g.Count(),
                AverageConfidence = g.Average(x => x.SelectedConfidence ?? 0),
                TotalReviewed = g.Count(x => x.ReviewStatus.HasValue && x.ReviewStatus.Value != AiReviewStatus.Pending),
                ConfirmedCorrect = g.Count(x => x.ReviewStatus.HasValue && x.ReviewStatus.Value == AiReviewStatus.ConfirmedCorrect)
            })
            .ToListAsync();

        var totalSystemInferences = metrics.Sum(m => m.TotalInferences);

        var dtos = metrics
            .Select(m => new AiAccuracyDto
            {
                SpeciesName = string.IsNullOrWhiteSpace(m.SpeciesName) ? "Unknown" : m.SpeciesName!,
                TotalInferences = m.TotalInferences,
                PercentageOfTotal = totalSystemInferences > 0 ? Math.Round((double)m.TotalInferences / totalSystemInferences * 100, 2) : 0,
                AverageConfidence = m.AverageConfidence,
                AccuracyRate = m.TotalReviewed > 0 ? (double)m.ConfirmedCorrect / m.TotalReviewed : 0,
                ReviewedCases = m.TotalReviewed,
                UnreviewedCases = m.TotalInferences - m.TotalReviewed
            })
            .OrderByDescending(x => x.TotalInferences)
            .ToList();

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            dtos);
    }

    public async Task<IServiceResult> GetSnakeIncidentTrackingAsync(Guid snakeId, AnalyticsSpecParams filter)
    {
        if (filter.StartDate == null && filter.EndDate == null)
        {
            var ictTz = AnalyticsQueryHelper.ResolveIctTimeZone();
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ictTz);
            filter.StartDate = now.AddDays(-30);
            filter.EndDate = now;
        }

        var snake = await _aiRepo.GetQueryable(false)
            .Where(x => x.SelectedSnakeId == snakeId)
            .Select(x => new { x.SelectedSnake })
            .FirstOrDefaultAsync();

        if (snake?.SelectedSnake == null)
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0004,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004),
                null);

        var snakeData = snake.SelectedSnake;

        var aiInferences = await AnalyticsQueryHelper.ApplyCreatedAtUtcRange(_aiRepo.GetQueryable(false), filter)
            .Where(x => x.SelectedSnakeId == snakeId)
            .Include(x => x.Incident)
            .ThenInclude(x => x.Victim)
            .Include(x => x.Candidates)
            .ThenInclude(c => c.Snake)
            .Include(x => x.SelectedSnake)
            .AsSplitQuery()
            .ToListAsync();

        if (aiInferences.Count == 0)
            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                new SnakeIncidentTrackingDto
                {
                    SnakeId = snakeId,
                    SnakeName = snakeData.CommonName,
                    ScientificName = snakeData.ScientificName,
                    ToxicityLevel = snakeData.ToxicityLevel.ToString(),
                    ToxinGroup = snakeData.ToxinGroup.ToString(),
                    TotalIncidentsIdentified = 0,
                    AccuracyRate = 0,
                    Incidents = new()
                });

        var incidentIds = aiInferences.Select(x => x.IncidentId).Distinct().ToList();

        var aiReviews = await _aiReviewRepo.GetQueryable(false)
            .Where(x => incidentIds.Contains(x.IncidentId))
            .Include(x => x.Reviewer)
            .Include(x => x.CorrectedSnake)
            .ToListAsync();

        var missions = await _rescueRepo.GetQueryable(false)
            .Where(x => incidentIds.Contains(x.IncidentId))
            .Include(x => x.Rescuer)
            .ToListAsync();

        var reviewByIncident = aiReviews
            .GroupBy(x => x.IncidentId)
            .ToDictionary(g => g.Key, g => g.FirstOrDefault());

        var missionByIncident = missions
            .GroupBy(x => x.IncidentId)
            .ToDictionary(g => g.Key, g => g.FirstOrDefault());

        var incidentDtos = aiInferences
            .GroupBy(x => x.IncidentId)
            .Select(group =>
            {
                var aiInference = group.First();
                var incident = aiInference.Incident;
                reviewByIncident.TryGetValue(incident.Id, out var review);
                missionByIncident.TryGetValue(incident.Id, out var mission);

                var candidates = group.SelectMany(x => x.Candidates)
                    .Select(c => new AiCandidateSnakeDto
                    {
                        SnakeId = c.SnakeId,
                        SnakeName = c.Snake?.CommonName ?? "Unknown",
                        ScientificName = c.Snake?.ScientificName ?? "Unknown",
                        Confidence = c.Confidence,
                        IsSelected = c.SnakeId == aiInference.SelectedSnakeId,
                        ToxicityLevel = c.Snake?.ToxicityLevel.ToString() ?? "Unknown",
                        ToxinGroup = c.Snake?.ToxinGroup.ToString() ?? "Unknown"
                    })
                    .OrderByDescending(x => x.Confidence)
                    .ToList();

                SnakeMissionDetailDto? missionDto = null;
                if (mission != null)
                {
                    missionDto = new SnakeMissionDetailDto
                    {
                        MissionId = mission.Id,
                        RescuerId = mission.RescuerId,
                        RescuerName = mission.Rescuer.FullName,
                        MissionStatus = mission.Status.ToString(),
                        MissionAcceptedAt = mission.CreatedAt,
                        MissionStartedAt = mission.StartedAt,
                        MissionArrivedAt = mission.ArrivedAt,
                        MissionCompletedAt = mission.CompletedAt,
                        RescuerNotes = mission.RescuerNotes,
                        PatientConditionAtHandover = mission.PatientConditionAtHandover,
                        ResponseTimeMinutes = mission.StartedAt.HasValue && mission.ArrivedAt.HasValue
                            ? (mission.ArrivedAt.Value - mission.StartedAt.Value).TotalMinutes
                            : 0,
                        HandoverTimeMinutes = mission.ArrivedAt.HasValue && mission.CompletedAt.HasValue
                            ? (mission.CompletedAt.Value - mission.ArrivedAt.Value).TotalMinutes
                            : 0,
                        TotalDurationMinutes = mission.CompletedAt.HasValue
                            ? (mission.CompletedAt.Value - mission.CreatedAt).TotalMinutes
                            : 0
                    };
                }

                SnakeRescuerReviewDto? reviewDto = null;
                if (review != null)
                {
                    reviewDto = new SnakeRescuerReviewDto
                    {
                        ReviewerId = review.ReviewerId ?? Guid.Empty,
                        ReviewerName = review.Reviewer.FullName,
                        ReviewStatus = review.ReviewStatus.ToString(),
                        CorrectedSnakeName = review.CorrectedSnake?.CommonName,
                        CorrectedSnakeId = review.CorrectedSnakeId,
                        ReviewComment = review.Comment,
                        ReviewedAt = review.ReviewedAt
                    };
                }

                return new SnakeIncidentDetailDto
                {
                    IncidentId = incident.Id,
                    IncidentCode = incident.Code,
                    IncidentDate = incident.CreatedAt,
                    IncidentLocation = new SnakeIncidentLocationDto
                    {
                        Latitude = incident.Location.Y,
                        Longitude = incident.Location.X
                    },
                    Severity = incident.PriorityLevel.ToString(),
                    IncidentDescription = incident.Description,
                    VictimId = incident.VictimId,
                    VictimName = incident.Victim.FullName,
                    VictimPhone = incident.Victim.Phone,
                    VictimAddress = incident.Victim.Address,
                    AiIdentification = new SnakeAiIdentificationDto
                    {
                        ModelName = aiInference.ModelName,
                        ModelVersion = aiInference.ModelVersion,
                        SelectedConfidence = aiInference.SelectedConfidence,
                        TopK = aiInference.TopK,
                        DecisionRule = aiInference.DecisionRule,
                        CreatedAt = aiInference.CreatedAt
                    },
                    TopCandidates = candidates,
                    RescuerReview = reviewDto,
                    Mission = missionDto
                };
            })
            .OrderByDescending(x => x.IncidentDate)
            .ToList();

        var confirmedCount = aiReviews.Count(x => x.ReviewStatus == AiReviewStatus.ConfirmedCorrect);
        var accuracyRate = aiInferences.Count > 0 ? (double)confirmedCount / aiInferences.Count : 0;

        var result = new SnakeIncidentTrackingDto
        {
            SnakeId = snakeId,
            SnakeName = snakeData.CommonName,
            ScientificName = snakeData.ScientificName,
            ToxicityLevel = snakeData.ToxicityLevel.ToString(),
            ToxinGroup = snakeData.ToxinGroup.ToString(),
            TotalIncidentsIdentified = aiInferences.Count,
            AccuracyRate = Math.Round(accuracyRate, 2),
            Incidents = incidentDtos
        };

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            result);
    }
}

