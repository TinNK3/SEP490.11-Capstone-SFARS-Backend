using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFARS.Application.Common;
using SFARS.Application.Dtos.AiInference;
using SFARS.Application.Dtos.AiReview;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;
using SFARS.Infrastructure.Configurations;
using SFARS.Infrastructure.Hubs;
using Hangfire;

namespace SFARS.Application.Services;

public class AiReviewService : IAiReviewService<SubmitAiReviewRequestDto, FirstAidStepDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemMessageService _msgService;
    private readonly IHubContext<LocationTrackingHub> _locationHub;
    private readonly ILogger<AiReviewService> _logger;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IFcmPushService _fcmService;
    private readonly MlopsOptions _mlopsOptions;

    public AiReviewService(
        IUnitOfWork unitOfWork,
        ISystemMessageService msgService,
        IHubContext<LocationTrackingHub> locationHub,
        ILogger<AiReviewService> logger,
        IBackgroundJobClient backgroundJobs,
        IFcmPushService fcmService,
        IOptions<MlopsOptions> mlopsOptions)
    {
        _unitOfWork = unitOfWork;
        _msgService = msgService;
        _locationHub = locationHub;
        _logger = logger;
        _backgroundJobs = backgroundJobs;
        _fcmService = fcmService;
        _mlopsOptions = mlopsOptions.Value;
    }

    public async Task<IServiceResult> SubmitReviewAsync(Guid incidentId, Guid rescuerId, SubmitAiReviewRequestDto request)
    {
        var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);
        if (incident == null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0002, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));

        if (!incident.CurrentAiReviewId.HasValue)
            return new ServiceResult(ResultCodeConst.AiReview_Fail0001, await _msgService.GetMessageAsync(ResultCodeConst.AiReview_Fail0001));

        var review = await _unitOfWork.Repository<AiInferenceReview, Guid>().GetByIdAsync(incident.CurrentAiReviewId.Value);
        if (review == null)
            return new ServiceResult(ResultCodeConst.AiReview_Fail0002, await _msgService.GetMessageAsync(ResultCodeConst.AiReview_Fail0002));

        if (review.ReviewerId != rescuerId)
            return new ServiceResult(ResultCodeConst.AiReview_Fail0003, await _msgService.GetMessageAsync(ResultCodeConst.AiReview_Fail0003));

        if (review.ReviewStatus == AiReviewStatus.ConfirmedCorrect || review.ReviewStatus == AiReviewStatus.Corrected || review.ReviewStatus == AiReviewStatus.UnableToAssess)
            return new ServiceResult(ResultCodeConst.AiReview_Fail0004, await _msgService.GetMessageAsync(ResultCodeConst.AiReview_Fail0004));

        // Load the inference + media to determine if this is a wound or snake review
        var inference = await _unitOfWork.Repository<AiInference, Guid>().GetByIdAsync(review.AiInferenceId);
        IncidentMedia? media = inference?.IncidentMediaId != null
            ? await _unitOfWork.Repository<IncidentMedia, Guid>().GetByIdAsync(inference.IncidentMediaId.Value)
            : null;
        bool isWoundReview = media?.MediaType == MediaType.BiteWoundPhoto;

        // Validate per review type
        if (!isWoundReview && request.ReviewStatus == AiReviewStatus.Corrected && request.CorrectedToxinGroup == null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));

        if (request.ReviewStatus == AiReviewStatus.UnableToAssess && request.UnableToAssessReasonChoice == null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));

        var oldStatus = review.ReviewStatus;

        // Apply common changes
        review.ReviewStatus = request.ReviewStatus;
        review.Comment = request.Comment;
        
        if (request.ReviewStatus != AiReviewStatus.Deferred)
            review.ReviewedAt = DateTime.UtcNow;

        review.UpdatedBy = rescuerId;
        review.UpdatedAt = DateTime.UtcNow;

        if (isWoundReview)
        {
            ApplyWoundReviewChanges(review, incident, inference!, request);
        }
        else
        {
            await ApplySnakeReviewChangesAsync(review, incident, inference, request);
        }

        incident.CurrentAiReviewStatus = request.ReviewStatus;

        // Audit log
        var audit = new AiReviewAuditLog
        {
            Id = Guid.NewGuid(),
            IncidentId = incidentId,
            ReviewId = review.Id,
            RescuerId = rescuerId,
            OldStatus = oldStatus,
            NewStatus = request.ReviewStatus,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = rescuerId
        };
        await _unitOfWork.Repository<AiReviewAuditLog, Guid>().AddAsync(audit);

        await _unitOfWork.SaveChangesAsync();

        // Realtime notification via SignalR (foreground)
        var reviewMessage = await _msgService.GetMessageAsync(ResultCodeConst.AiReview_Success0001);
        var notificationPayload = new AiReviewCompletedNotificationDto
        {
            IncidentId = incidentId,
            Message = reviewMessage,
            ReviewStatus = request.ReviewStatus.ToString(),
            EffectiveToxinGroup = incident.HumanReviewedToxinGroup?.ToString() ?? ToxinGroup.Unknown.ToString(),
            RequiresFirstAidRefresh = (request.ReviewStatus == AiReviewStatus.ConfirmedCorrect || request.ReviewStatus == AiReviewStatus.Corrected)
        };
        await _locationHub.Clients
            .GroupExcept(LocationConstants.SignalRGroupPrefix + incidentId, new[] { rescuerId.ToString() })
            .SendAsync(LocationConstants.SignalRAiReviewed, notificationPayload);

        // FCM push notification to patient (background fallback)
        var fcmTitle = "Hệ thống - Kết quả AI";
        var fcmData = new Dictionary<string, string>
        {
            { "type", "ai_review_completed" },
            { "incidentId", incidentId.ToString() },
            { "reviewStatus", request.ReviewStatus.ToString() }
        };
        await _fcmService.SendToUserAsync(incident.VictimId, fcmTitle, reviewMessage, fcmData);

        // Check retrain threshold (only for verified reviews)
        if (request.ReviewStatus == AiReviewStatus.ConfirmedCorrect || request.ReviewStatus == AiReviewStatus.Corrected)
        {
            if (isWoundReview)
                await CheckAndTriggerWoundRetrainAsync();
            else
                await CheckAndTriggerSnakeRetrainAsync();
        }

        return new ServiceResult(ResultCodeConst.AiReview_Success0001, await _msgService.GetMessageAsync(ResultCodeConst.AiReview_Success0001), request.ReviewStatus.ToString());
    }

    // Snake review: sets corrected species / toxin group and recalculates priority
    private async Task ApplySnakeReviewChangesAsync(
        AiInferenceReview review, Incident incident, AiInference? inference, SubmitAiReviewRequestDto request)
    {
        switch (request.ReviewStatus)
        {
            case AiReviewStatus.ConfirmedCorrect:
                if (inference == null)
                    break;
                incident.HumanReviewedToxinGroup = inference.SelectedToxinGroup;
                incident.HumanReviewedSnakeId = inference.SelectedSnakeId;
                review.CorrectedSnakeId = null;
                review.CorrectedToxinGroup = null;
                review.UnableToAssessReasonChoice = null;
                break;
            case AiReviewStatus.Corrected:
                review.CorrectedSnakeId = request.CorrectedSnakeId;
                review.CorrectedToxinGroup = request.CorrectedToxinGroup;
                review.UnableToAssessReasonChoice = null;
                incident.HumanReviewedToxinGroup = request.CorrectedToxinGroup;
                incident.HumanReviewedSnakeId = request.CorrectedSnakeId;
                break;
            case AiReviewStatus.UnableToAssess:
                review.UnableToAssessReasonChoice = request.UnableToAssessReasonChoice;
                review.CorrectedSnakeId = null;
                review.CorrectedToxinGroup = null;
                incident.HumanReviewedToxinGroup = null;
                incident.HumanReviewedSnakeId = null;
                break;
            case AiReviewStatus.Deferred:
                review.UnableToAssessReasonChoice = null;
                review.CorrectedSnakeId = null;
                review.CorrectedToxinGroup = null;
                incident.HumanReviewedToxinGroup = null;
                incident.HumanReviewedSnakeId = null;
                break;
        }

        // Recalculate priority based on snake toxicity
        switch (request.ReviewStatus)
        {
            case AiReviewStatus.ConfirmedCorrect:
                incident.PriorityLevel = AiInferenceConstants.DetermineIncidentPriority(
                    isSkip: false, isLowConfidence: false,
                    inference?.SelectedSnakeId.HasValue == true
                        ? (await _unitOfWork.Repository<Snake, Guid>().GetByIdAsync(inference.SelectedSnakeId!.Value))?.ToxicityLevel
                        : null);
                break;
            case AiReviewStatus.Corrected:
                incident.PriorityLevel = AiInferenceConstants.DetermineIncidentPriority(
                    isSkip: false, isLowConfidence: false,
                    request.CorrectedSnakeId.HasValue
                        ? (await _unitOfWork.Repository<Snake, Guid>().GetByIdAsync(request.CorrectedSnakeId.Value))?.ToxicityLevel
                        : null);
                break;
            default:
                incident.PriorityLevel = SeverityLevel.High;
                break;
        }
    }

    // Wound review: for wound photos, no snake species info to correct.
    // Priority stays High (wound context — always requires urgent attention).
    private void ApplyWoundReviewChanges(
        AiInferenceReview review, Incident incident, AiInference inference, SubmitAiReviewRequestDto request)
    {
        switch (request.ReviewStatus)
        {
            case AiReviewStatus.ConfirmedCorrect:
                // AI was correct - no fields to override
                review.CorrectedSnakeId = null;
                review.CorrectedToxinGroup = null;
                review.UnableToAssessReasonChoice = null;
                break;
            case AiReviewStatus.Corrected:
                // AI was wrong - ground truth is the inverse of AI prediction.
                // No extra fields needed — the inverse is derived at export time.
                review.CorrectedSnakeId = null;
                review.CorrectedToxinGroup = null;
                review.UnableToAssessReasonChoice = null;
                break;
            case AiReviewStatus.UnableToAssess:
                review.UnableToAssessReasonChoice = request.UnableToAssessReasonChoice;
                review.CorrectedSnakeId = null;
                review.CorrectedToxinGroup = null;
                break;
            case AiReviewStatus.Deferred:
                review.UnableToAssessReasonChoice = null;
                review.CorrectedSnakeId = null;
                review.CorrectedToxinGroup = null;
                break;
        }

        // Wound reviews: keep priority High regardless (wound = always urgent)
        incident.PriorityLevel = SeverityLevel.High;
    }

    private async Task CheckAndTriggerSnakeRetrainAsync()
    {
        try
        {
            int threshold = _mlopsOptions.AutoRetrainThreshold;

            var lastRetrain = await _unitOfWork.Repository<RetrainHistory, Guid>().GetQueryable(tracked: false)
                .Where(x => x.PipelineType == RetrainPipelineType.SnakeSpecies)
                .OrderByDescending(x => x.StartedAt)
                .FirstOrDefaultAsync(x => x.Status == RetrainStatus.Success || x.Status == RetrainStatus.Pending || x.Status == RetrainStatus.Training);

            DateTime? since = lastRetrain?.StartedAt;

            // Count new verified SNAKE reviews since last retrain
            var newSamplesCount = await (
                from r in _unitOfWork.Repository<AiInferenceReview, Guid>().GetQueryable(tracked: false)
                join ai in _unitOfWork.Repository<AiInference, Guid>().GetQueryable(tracked: false)
                    on r.AiInferenceId equals ai.Id
                join m in _unitOfWork.Repository<IncidentMedia, Guid>().GetQueryable(tracked: false)
                    on ai.IncidentMediaId equals m.Id
                where (r.ReviewStatus == AiReviewStatus.ConfirmedCorrect || r.ReviewStatus == AiReviewStatus.Corrected)
                      && m.MediaType == MediaType.SnakePhoto
                      && (since == null || r.ReviewedAt > since)
                select r.Id
            ).CountAsync();

            _logger.LogInformation("MLOps Snake Check: {Count}/{Threshold} verified samples since last retrain.", newSamplesCount, threshold);

            if (newSamplesCount >= threshold)
            {
                bool isRunning = await _unitOfWork.Repository<RetrainHistory, Guid>().GetQueryable(tracked: false)
                    .AnyAsync(x => x.PipelineType == RetrainPipelineType.SnakeSpecies
                                   && (x.Status == RetrainStatus.Pending || x.Status == RetrainStatus.Training || x.Status == RetrainStatus.ExportingData));

                if (!isRunning)
                {
                    _logger.LogInformation("Snake retrain threshold reached! Queueing MLOps pipeline.");
                    _backgroundJobs.Enqueue<IRetrainOrchestrationService>(s => s.TriggerRetrainAsync(since));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check or trigger snake auto-retrain pipeline.");
        }
    }

    private async Task CheckAndTriggerWoundRetrainAsync()
    {
        try
        {
            int threshold = _mlopsOptions.WoundAutoRetrainThreshold;

            var lastRetrain = await _unitOfWork.Repository<RetrainHistory, Guid>().GetQueryable(tracked: false)
                .Where(x => x.PipelineType == RetrainPipelineType.WoundClassification)
                .OrderByDescending(x => x.StartedAt)
                .FirstOrDefaultAsync(x => x.Status == RetrainStatus.Success || x.Status == RetrainStatus.Pending || x.Status == RetrainStatus.Training);

            DateTime? since = lastRetrain?.StartedAt;

            // Count new verified WOUND reviews since last retrain
            var newSamplesCount = await (
                from r in _unitOfWork.Repository<AiInferenceReview, Guid>().GetQueryable(tracked: false)
                join ai in _unitOfWork.Repository<AiInference, Guid>().GetQueryable(tracked: false)
                    on r.AiInferenceId equals ai.Id
                join m in _unitOfWork.Repository<IncidentMedia, Guid>().GetQueryable(tracked: false)
                    on ai.IncidentMediaId equals m.Id
                where (r.ReviewStatus == AiReviewStatus.ConfirmedCorrect || r.ReviewStatus == AiReviewStatus.Corrected)
                      && m.MediaType == MediaType.BiteWoundPhoto
                      && ai.IsSnakeBite != null
                      && (since == null || r.ReviewedAt > since)
                select r.Id
            ).CountAsync();

            _logger.LogInformation("MLOps Wound Check: {Count}/{Threshold} verified samples since last retrain.", newSamplesCount, threshold);

            if (newSamplesCount >= threshold)
            {
                bool isRunning = await _unitOfWork.Repository<RetrainHistory, Guid>().GetQueryable(tracked: false)
                    .AnyAsync(x => x.PipelineType == RetrainPipelineType.WoundClassification
                                   && (x.Status == RetrainStatus.Pending || x.Status == RetrainStatus.Training || x.Status == RetrainStatus.ExportingData));

                if (!isRunning)
                {
                    _logger.LogInformation("Wound retrain threshold reached! Queueing MLOps pipeline.");
                    _backgroundJobs.Enqueue<IRetrainOrchestrationService>(s => s.TriggerWoundRetrainAsync(since));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check or trigger wound auto-retrain pipeline.");
        }
    }

    public async Task<(List<FirstAidStepDto> steps, List<string> prohibitions)> GetEffectiveFirstAidProtocolAsync(Incident incident)
    {
        var activeGroup = ToxinGroup.Unknown;

        if (incident.CurrentAiReviewStatus == AiReviewStatus.ConfirmedCorrect ||
            incident.CurrentAiReviewStatus == AiReviewStatus.Corrected)
        {
            activeGroup = incident.HumanReviewedToxinGroup ?? ToxinGroup.Unknown;
        }

        var firstAidSteps = await GetFirstAidStepsAsync(activeGroup);
        var prohibitions = await GetProhibitionsAsync();

        return (firstAidSteps, prohibitions);
    }

    private async Task<List<FirstAidStepDto>> GetFirstAidStepsAsync(ToxinGroup toxinGroup)
    {
        var spec = new BaseSpecification<FirstAidDetail>(f => 
            f.ToxinGroup == toxinGroup 
            && f.SnakeId == null 
            && f.LanguageCode == SystemLanguage.Vietnamese);
            
        spec.AddOrderBy(f => f.StepOrder);
        
        var steps = await _unitOfWork.Repository<FirstAidDetail, Guid>()
            .GetAllWithSpecAsync(spec, tracked: false);

        return steps.Select(f => new FirstAidStepDto
        {
            StepOrder = f.StepOrder,
            Title = f.Title,
            Content = f.ContentMarkdown,
            ImageUrl = f.ImageUrl
        }).ToList();
    }

    private async Task<List<string>> GetProhibitionsAsync()
    {
        var spec = new BaseSpecification<FirstAidDetail>(f => 
            f.ToxinGroup == ToxinGroup.GeneralProhibition 
            && f.SnakeId == null 
            && f.LanguageCode == SystemLanguage.Vietnamese);
            
        spec.AddOrderBy(f => f.StepOrder);

        var items = await _unitOfWork.Repository<FirstAidDetail, Guid>()
            .GetAllWithSpecAsync(spec, tracked: false);

        return items.Select(f => f.Title ?? "").ToList();
    }
}