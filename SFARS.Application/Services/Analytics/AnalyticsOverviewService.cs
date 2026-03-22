using Microsoft.EntityFrameworkCore;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Analytics;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Application.Services.Analytics;

public interface IAnalyticsOverviewService
{
    Task<IServiceResult> GetOverviewMetricsAsync(AnalyticsSpecParams filter);
}

public class AnalyticsOverviewService : IAnalyticsOverviewService
{
    private readonly IGenericRepository<Incident, Guid> _incidentRepo;
    private readonly IGenericRepository<RescueMission, Guid> _rescueRepo;
    private readonly IGenericRepository<Transaction, Guid> _transactionRepo;
    private readonly IGenericRepository<User, Guid> _userRepo;
    private readonly IGenericRepository<AiInferenceReview, Guid> _aiReviewRepo;
    private readonly ISystemMessageService _msgService;

    public AnalyticsOverviewService(
        IGenericRepository<Incident, Guid> incidentRepo,
        IGenericRepository<RescueMission, Guid> rescueRepo,
        IGenericRepository<Transaction, Guid> transactionRepo,
        IGenericRepository<User, Guid> userRepo,
        IGenericRepository<AiInferenceReview, Guid> aiReviewRepo,
        ISystemMessageService msgService)
    {
        _incidentRepo = incidentRepo;
        _rescueRepo = rescueRepo;
        _transactionRepo = transactionRepo;
        _userRepo = userRepo;
        _aiReviewRepo = aiReviewRepo;
        _msgService = msgService;
    }

    public async Task<IServiceResult> GetOverviewMetricsAsync(AnalyticsSpecParams filter)
    {
        var incidentsQuery = AnalyticsQueryHelper.ApplyCreatedAtUtcRange(_incidentRepo.GetQueryable(false), filter);
        var rescueQuery = AnalyticsQueryHelper.ApplyCreatedAtUtcRange(_rescueRepo.GetQueryable(false), filter);
        var usersQuery = AnalyticsQueryHelper.ApplyCreatedAtUtcRange(_userRepo.GetQueryable(false), filter);
        var aiReviewQuery = AnalyticsQueryHelper.ApplyCreatedAtUtcRange(_aiReviewRepo.GetQueryable(false), filter);

        var transactionQuery = _transactionRepo.GetQueryable(false);
        var (startUtc, endUtcExclusive) = AnalyticsQueryHelper.NormalizeUtcRange(filter);
        if (startUtc.HasValue)
            transactionQuery = transactionQuery.Where(x => (x.TransactionDate ?? x.CreatedAt) >= startUtc.Value);
        if (endUtcExclusive.HasValue)
            transactionQuery = transactionQuery.Where(x => (x.TransactionDate ?? x.CreatedAt) < endUtcExclusive.Value);

        var incidentAgg = await incidentsQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalIncidents = g.Count(),
                CriticalIncidents = g.Count(i => i.PriorityLevel == SeverityLevel.Critical)
            })
            .FirstOrDefaultAsync();

        var rescueAgg = await rescueQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ActiveRescues = g.Count(x =>
                    x.Status == RescueStatus.Pending ||
                    x.Status == RescueStatus.Accepted ||
                    x.Status == RescueStatus.Reassigned),
                CompletedRescues = g.Count(x => x.Status == RescueStatus.Completed),
                FailedRescues = g.Count(x => x.Status == RescueStatus.Rejected),
                TotalMissions = g.Count(),
                AvgResponseTimeMinutes = g.Where(x => x.Status == RescueStatus.Completed && x.StartedAt.HasValue && x.ArrivedAt.HasValue)
                    .Average(x => (double?)EF.Functions.DateDiffMinute(x.StartedAt, x.ArrivedAt)),
                AvgResolutionTimeMinutes = g.Where(x => x.Status == RescueStatus.Completed && x.StartedAt.HasValue && x.CompletedAt.HasValue)
                    .Average(x => (double?)EF.Functions.DateDiffMinute(x.StartedAt, x.CompletedAt))
            })
            .FirstOrDefaultAsync();

        var totalDonations = await transactionQuery
            .Where(x => x.Status == PaymentStatus.Paid)
            .Select(x => (decimal?)x.Amount)
            .SumAsync();

        var userAgg = await usersQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalUsers = g.Count(u => u.Status != UserStatus.Deleted),
                AvailableRescuers = g.Count(u =>
                    u.Status == UserStatus.Active &&
                    u.IsOnline &&
                    u.UserRoles.Any(ur => ur.Role.RoleName == UserTypeConstants.Rescuer))
            })
            .FirstOrDefaultAsync();

        var aiAgg = await aiReviewQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ReviewedCount = g.Count(x =>
                    x.ReviewStatus != AiReviewStatus.Pending &&
                    x.ReviewStatus != AiReviewStatus.Deferred &&
                    x.ReviewStatus != AiReviewStatus.UnableToAssess &&
                    x.ReviewStatus != AiReviewStatus.Abandoned),
                ConfirmedCorrect = g.Count(x => x.ReviewStatus == AiReviewStatus.ConfirmedCorrect)
            })
            .FirstOrDefaultAsync();

        var totalMissions = rescueAgg?.TotalMissions ?? 0;
        var completedRescues = rescueAgg?.CompletedRescues ?? 0;
        var aiReviewedCount = aiAgg?.ReviewedCount ?? 0;

        var dto = new DashboardOverviewDto
        {
            TotalIncidents = incidentAgg?.TotalIncidents ?? 0,
            ActiveRescues = rescueAgg?.ActiveRescues ?? 0,
            TotalDonations = totalDonations ?? 0,
            TotalUsers = userAgg?.TotalUsers ?? 0,
            CompletedRescues = completedRescues,
            FailedRescues = rescueAgg?.FailedRescues ?? 0,
            CriticalIncidents = incidentAgg?.CriticalIncidents ?? 0,
            AvgResponseTimeMinutes = rescueAgg?.AvgResponseTimeMinutes ?? 0d,
            AvgResolutionTimeMinutes = rescueAgg?.AvgResolutionTimeMinutes ?? 0d,
            SuccessRate = totalMissions > 0 ? Math.Round((double)completedRescues / totalMissions * 100, 2) : 0d,
            AvailableRescuers = userAgg?.AvailableRescuers ?? 0,
            AiAccuracy = aiReviewedCount > 0
                ? Math.Round((double)(aiAgg?.ConfirmedCorrect ?? 0) / aiReviewedCount * 100, 2)
                : 0d
        };

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            dto);
    }
}

