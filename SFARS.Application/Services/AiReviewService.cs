using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFARS.Application.Common;
using SFARS.Application.Dtos.AiInference;
using SFARS.Application.Dtos.AiReview;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;
using SFARS.Infrastructure.Configurations;

namespace SFARS.Application.Services;

public class AiReviewService : IAiReviewService<SubmitAiReviewRequestDto, FirstAidStepDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemMessageService _msgService;
    private readonly ILogger<AiReviewService> _logger;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly MlopsOptions _mlopsOptions;
    private readonly IFileStorageService _fileStorageService;

    public AiReviewService(
        IUnitOfWork unitOfWork,
        ISystemMessageService msgService,
        ILogger<AiReviewService> logger,
        IBackgroundJobClient backgroundJobs,
        IOptions<MlopsOptions> mlopsOptions,
        IFileStorageService fileStorageService)
    {
        _unitOfWork = unitOfWork;
        _msgService = msgService;
        _logger = logger;
        _backgroundJobs = backgroundJobs;
        _mlopsOptions = mlopsOptions.Value;
        _fileStorageService = fileStorageService;
    }

    /// <summary>
    /// Rescuer submits their assessment — updates ONLY Incident snapshot fields.
    /// AiInferenceReview record stays in Pending.
    /// The review is optional: rescuer may close the incident without submitting.
    /// </summary>
    public async Task<IServiceResult> SubmitReviewAsync(Guid incidentId, Guid rescuerId, SubmitAiReviewRequestDto request,
        Stream? snakeImageStream = null, string? snakeImageFileName = null, string? snakeImageContentType = null)
    {
        if (request.ReviewStatus != AiReviewStatus.ConfirmedCorrect && request.ReviewStatus != AiReviewStatus.Corrected && request.ReviewStatus != AiReviewStatus.UnableToAssess)
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));

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

        // If admin has already finalized this review, rescuer cannot modify
        if (review.AdminReviewerId.HasValue)
            return new ServiceResult(ResultCodeConst.AiReview_Fail0004, await _msgService.GetMessageAsync(ResultCodeConst.AiReview_Fail0004));

        // Load the inference + media to determine if this is a wound or snake review
        var inference = await _unitOfWork.Repository<AiInference, Guid>().GetByIdAsync(review.AiInferenceId);
        IncidentMedia? media = inference?.IncidentMediaId != null
            ? await _unitOfWork.Repository<IncidentMedia, Guid>().GetByIdAsync(inference.IncidentMediaId.Value)
            : null;
        bool isWoundReview = media?.MediaType == MediaType.BiteWoundPhoto;

        // Validate per review type
        if (!isWoundReview && request.ReviewStatus == AiReviewStatus.Corrected)
        {
            if (request.CorrectedSnakeId.HasValue)
            {
                var correctedSnake = await _unitOfWork.Repository<Snake, Guid>().GetByIdAsync(request.CorrectedSnakeId.Value);
                if (correctedSnake != null)
                {
                    request.CorrectedToxinGroup = correctedSnake.ToxinGroup;
                }
                else if (request.CorrectedToxinGroup == null)
                {
                    return new ServiceResult(ResultCodeConst.SYS_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));
                }
            }
            else if (request.CorrectedToxinGroup == null)
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));
            }
        }

        if (request.ReviewStatus == AiReviewStatus.UnableToAssess && request.UnableToAssessReasonChoice == null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));

        // Handle new snake image upload (create inactive snake)
        if (request.ReviewStatus == AiReviewStatus.Corrected && snakeImageStream != null)
        {
            var newSnakeId = await CreateInactiveSnakeWithImageAsync(
                snakeImageStream,
                snakeImageFileName!,
                snakeImageContentType!,
                request.CorrectedToxinGroup,
                request.NewSnakeCommonName,
                rescuerId);

            request.CorrectedSnakeId = newSnakeId;
        }

        // Update Incident snapshot fields ONLY
        incident.CurrentAiReviewStatus = request.ReviewStatus;
        incident.UpdatedBy = rescuerId;
        incident.UpdatedAt = DateTime.UtcNow;

        if (isWoundReview)
        {
            ApplyWoundSnapshotChanges(incident, request);
        }
        else
        {
            await ApplySnakeSnapshotChangesAsync(incident, inference, request);
        }

        // Audit log
        var audit = new AiReviewAuditLog
        {
            Id = Guid.NewGuid(),
            IncidentId = incidentId,
            ReviewId = review.Id,
            RescuerId = rescuerId,
            OldStatus = review.ReviewStatus,
            NewStatus = request.ReviewStatus,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = rescuerId
        };
        await _unitOfWork.Repository<AiReviewAuditLog, Guid>().AddAsync(audit);

        await _unitOfWork.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.AiReview_Success0001, await _msgService.GetMessageAsync(ResultCodeConst.AiReview_Success0001), request.ReviewStatus.ToString());
    }

    /// <summary>
    /// Admin finalizes the AI review — this is the ONLY path that updates AiInferenceReview.
    /// Triggers retrain check only on AdminConfirmed.
    /// </summary>
    public async Task<IServiceResult> AdminReviewAsync(Guid incidentId, Guid adminId, SubmitAiReviewRequestDto request,
        Stream? snakeImageStream = null, string? snakeImageFileName = null, string? snakeImageContentType = null)
    {

        if (request.CorrectedSnakeId.HasValue && !request.CorrectedToxinGroup.HasValue)
        {
            var correctedSnake = await _unitOfWork.Repository<Snake, Guid>().GetByIdAsync(request.CorrectedSnakeId.Value);
            if (correctedSnake != null)
            {
                request.CorrectedToxinGroup = correctedSnake.ToxinGroup;
            }
        }

        // Validate decision
        if (request.ReviewStatus != AiReviewStatus.ConfirmedCorrect && request.ReviewStatus != AiReviewStatus.Corrected && request.ReviewStatus != AiReviewStatus.UnableToAssess)
        {
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));
        }

        var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);
        if (incident == null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0002, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));

        if (!incident.CurrentAiReviewId.HasValue)
            return new ServiceResult(ResultCodeConst.AiReview_Fail0001, await _msgService.GetMessageAsync(ResultCodeConst.AiReview_Fail0001));

        var review = await _unitOfWork.Repository<AiInferenceReview, Guid>().GetByIdAsync(incident.CurrentAiReviewId.Value);
        if (review == null)
            return new ServiceResult(ResultCodeConst.AiReview_Fail0002, await _msgService.GetMessageAsync(ResultCodeConst.AiReview_Fail0002));

        // Prevent double-finalization
        if (review.AdminReviewerId.HasValue)
            return new ServiceResult(ResultCodeConst.AiReview_Fail0006, await _msgService.GetMessageAsync(ResultCodeConst.AiReview_Fail0006));

        // Load inference + media for context
        var inference = await _unitOfWork.Repository<AiInference, Guid>().GetByIdAsync(review.AiInferenceId);
        IncidentMedia? media = inference?.IncidentMediaId != null
            ? await _unitOfWork.Repository<IncidentMedia, Guid>().GetByIdAsync(inference.IncidentMediaId.Value)
            : null;
        bool isWoundReview = media?.MediaType == MediaType.BiteWoundPhoto;

        var oldStatus = review.ReviewStatus;

        // Handle new snake image upload from admin
        if (request.ReviewStatus == AiReviewStatus.Corrected
            && snakeImageStream != null)
        {
            var newSnakeId = await CreateInactiveSnakeWithImageAsync(
                snakeImageStream,
                snakeImageFileName!,
                snakeImageContentType!,
                request.CorrectedToxinGroup,
                request.NewSnakeCommonName,
                adminId);

            request.CorrectedSnakeId = newSnakeId;
        }

        // Finalize AiInferenceReview
        review.AdminReviewerId = adminId;
        review.AdminComment = request.Comment;
        review.ReviewedAt = DateTime.UtcNow;
        review.UpdatedBy = adminId;
        review.UpdatedAt = DateTime.UtcNow;

        bool rescuerHasReviewed = incident.CurrentAiReviewStatus.HasValue
            && incident.CurrentAiReviewStatus != AiReviewStatus.Pending;

        review.ReviewStatus = request.ReviewStatus;

        if (request.ReviewStatus == AiReviewStatus.ConfirmedCorrect)
        {
            if (rescuerHasReviewed && incident.CurrentAiReviewStatus == AiReviewStatus.Corrected)
            {
                // Admin approves what the rescuer corrected → adopt rescuer's data
                review.CorrectedSnakeId = incident.HumanReviewedSnakeId;
                review.CorrectedToxinGroup = incident.HumanReviewedToxinGroup;
                if (isWoundReview) review.IsConfirmedWoundSnakeBite = incident.HumanConfirmedSnakeBite;
            }
            else
            {
                // Admin confirms the original AI result (rescuer also confirmed or never reviewed)
                review.CorrectedSnakeId = null;
                review.CorrectedToxinGroup = null;
                if (isWoundReview) review.IsConfirmedWoundSnakeBite = request.IsConfirmedSnakeBite ?? review.IsConfirmedWoundSnakeBite;
            }
        }
        else if (request.ReviewStatus == AiReviewStatus.Corrected)
        {
            // Admin provides their own correction (overrides everything)
            review.CorrectedSnakeId = request.CorrectedSnakeId;
            review.CorrectedToxinGroup = request.CorrectedToxinGroup;
            if (isWoundReview) review.IsConfirmedWoundSnakeBite = request.IsConfirmedSnakeBite;
        }
        else // UnableToAssess
        {
            review.CorrectedSnakeId = null;
            review.CorrectedToxinGroup = null;
            if (isWoundReview) review.IsConfirmedWoundSnakeBite = null;
        }
        review.UnableToAssessReasonChoice = request.UnableToAssessReasonChoice;

        // Audit log
        var audit = new AiReviewAuditLog
        {
            Id = Guid.NewGuid(),
            IncidentId = incidentId,
            ReviewId = review.Id,
            RescuerId = adminId,
            OldStatus = oldStatus,
            NewStatus = review.ReviewStatus,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = adminId
        };
        await _unitOfWork.Repository<AiReviewAuditLog, Guid>().AddAsync(audit);

        await _unitOfWork.SaveChangesAsync();

        // Trigger retrain check only when Confirmed or Corrected
        bool isVerifiedSample = (request.ReviewStatus == AiReviewStatus.ConfirmedCorrect) 
            || (request.ReviewStatus == AiReviewStatus.Corrected);

        if (isVerifiedSample)
        {
            if (isWoundReview)
                _backgroundJobs.Enqueue<AiReviewService>(s => s.CheckAndTriggerWoundRetrainAsync());
            else
                _backgroundJobs.Enqueue<AiReviewService>(s => s.CheckAndTriggerSnakeRetrainAsync());
        }

        return new ServiceResult(ResultCodeConst.AiReview_Success0002, await _msgService.GetMessageAsync(ResultCodeConst.AiReview_Success0002), request.ReviewStatus.ToString());
    }

    // Rescuer Snapshot Helpers

    private async Task ApplySnakeSnapshotChangesAsync(
        Incident incident, AiInference? inference, SubmitAiReviewRequestDto request)
    {
        switch (request.ReviewStatus)
        {
            case AiReviewStatus.ConfirmedCorrect:
                if (inference != null)
                {
                    incident.HumanReviewedToxinGroup = inference.SelectedToxinGroup;
                    incident.HumanReviewedSnakeId = inference.SelectedSnakeId;
                }
                break;
            case AiReviewStatus.Corrected:
                incident.HumanReviewedToxinGroup = request.CorrectedToxinGroup;
                incident.HumanReviewedSnakeId = request.CorrectedSnakeId;
                break;
            case AiReviewStatus.UnableToAssess:
                incident.HumanReviewedToxinGroup = null;
                incident.HumanReviewedSnakeId = null;
                break;
        }

        // Recalculate priority
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

    private void ApplyWoundSnapshotChanges(Incident incident, SubmitAiReviewRequestDto request)
    {
        if (request.ReviewStatus == AiReviewStatus.ConfirmedCorrect || request.ReviewStatus == AiReviewStatus.Corrected)
        {
            incident.HumanConfirmedSnakeBite = request.IsConfirmedSnakeBite;
        }
        else
        {
            incident.HumanConfirmedSnakeBite = null;
        }

        incident.PriorityLevel = SeverityLevel.High;
    }

    // Snake Image Upload Helper

    /// <summary>
    /// Creates a new inactive Snake record with a single uploaded image.
    /// Admin must later complete the snake's info via the Snake management API.
    /// </summary>
    private async Task<Guid> CreateInactiveSnakeWithImageAsync(
        Stream imageStream, string fileName, string contentType,
        ToxinGroup? toxinGroup, string? commonName, Guid createdBy)
    {
        var snake = new Snake
        {
            Id = Guid.NewGuid(),
            CommonName = string.IsNullOrWhiteSpace(commonName) ? AiInferenceConstants.InactiveSnakeCommonName : commonName,
            ScientificName = $"{AiInferenceConstants.InactiveSnakeScientificNamePrefix}{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}",
            ToxicityLevel = default, // Admin must update this
            ToxinGroup = toxinGroup ?? ToxinGroup.Unknown,
            IsActive = false,
            Note = AiInferenceConstants.InactiveSnakeNote,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        await _unitOfWork.Repository<Snake, Guid>().AddAsync(snake);

        // Upload image to Cloudinary
        var uploadResult = await _fileStorageService.UploadAsync(imageStream, fileName, "snakes", contentType);

        var snakeImage = new SnakeImage
        {
            Id = Guid.NewGuid(),
            SnakeId = snake.Id,
            ImageUrl = uploadResult.Url,
            IsPrimary = true
        };

        await _unitOfWork.Repository<SnakeImage, Guid>().AddAsync(snakeImage);

        _logger.LogInformation("Created inactive snake {SnakeId} with image from AI Review by user {UserId}", snake.Id, createdBy);

        return snake.Id;
    }

    // Auto-Retrain Checks

    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task CheckAndTriggerSnakeRetrainAsync()
    {
        try
        {
            int threshold = _mlopsOptions.AutoRetrainThreshold;

            var lastRetrain = await _unitOfWork.Repository<RetrainHistory, Guid>()
                .GetQueryable(tracked: false)
                .AsNoTracking()
                .Where(x => x.PipelineType == RetrainPipelineType.SnakeSpecies)
                .OrderByDescending(x => x.StartedAt)
                .FirstOrDefaultAsync(x => x.Status == RetrainStatus.Success || 
                                          x.Status == RetrainStatus.Pending || 
                                          x.Status == RetrainStatus.Training);

            DateTime? since = lastRetrain?.StartedAt;

            var newSamplesCount = await (
                from r in _unitOfWork.Repository<AiInferenceReview, Guid>().GetQueryable(tracked: false).AsNoTracking()
                join ai in _unitOfWork.Repository<AiInference, Guid>().GetQueryable(tracked: false).AsNoTracking() on r.AiInferenceId equals ai.Id
                join m in _unitOfWork.Repository<IncidentMedia, Guid>().GetQueryable(tracked: false).AsNoTracking() on ai.IncidentMediaId equals m.Id
                where (r.ReviewStatus == AiReviewStatus.ConfirmedCorrect || r.ReviewStatus == AiReviewStatus.Corrected)
                      && m.MediaType == MediaType.SnakePhoto
                      && (since == null || r.ReviewedAt > since)
                select r.Id
            ).CountAsync();

            _logger.LogInformation("MLOps Snake Progress: {Count}/{Threshold} verified samples since {Since}", 
                newSamplesCount, threshold, since?.ToString() ?? "Project Start");

            if (newSamplesCount >= threshold)
            {
                bool isRunning = await _unitOfWork.Repository<RetrainHistory, Guid>()
                    .GetQueryable(tracked: false)
                    .AsNoTracking()
                    .AnyAsync(x => x.PipelineType == RetrainPipelineType.SnakeSpecies
                                   && (x.Status == RetrainStatus.Pending || 
                                       x.Status == RetrainStatus.Training || 
                                       x.Status == RetrainStatus.ExportingData));

                if (!isRunning)
                {
                    _logger.LogWarning("AUTO-RETRAIN: Snake threshold reached! Enqueueing pipeline...");
                    _backgroundJobs.Enqueue<IRetrainOrchestrationService>(s => s.TriggerRetrainAsync(since));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check or trigger snake auto-retrain pipeline.");
        }
    }

    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task CheckAndTriggerWoundRetrainAsync()
    {
        try
        {
            int threshold = _mlopsOptions.WoundAutoRetrainThreshold;

            var lastRetrain = await _unitOfWork.Repository<RetrainHistory, Guid>()
                .GetQueryable(tracked: false)
                .AsNoTracking()
                .Where(x => x.PipelineType == RetrainPipelineType.WoundClassification)
                .OrderByDescending(x => x.StartedAt)
                .FirstOrDefaultAsync(x => x.Status == RetrainStatus.Success || 
                                          x.Status == RetrainStatus.Pending || 
                                          x.Status == RetrainStatus.Training);

            DateTime? since = lastRetrain?.StartedAt;

            var newSamplesCount = await (
                from r in _unitOfWork.Repository<AiInferenceReview, Guid>().GetQueryable(tracked: false).AsNoTracking()
                join ai in _unitOfWork.Repository<AiInference, Guid>().GetQueryable(tracked: false).AsNoTracking() on r.AiInferenceId equals ai.Id
                join m in _unitOfWork.Repository<IncidentMedia, Guid>().GetQueryable(tracked: false).AsNoTracking() on ai.IncidentMediaId equals m.Id
                where (r.ReviewStatus == AiReviewStatus.ConfirmedCorrect || r.ReviewStatus == AiReviewStatus.Corrected)
                      && m.MediaType == MediaType.BiteWoundPhoto
                      && ai.IsSnakeBite != null
                      && (since == null || r.ReviewedAt > since)
                select r.Id
            ).CountAsync();

            _logger.LogInformation("MLOps Wound Progress: {Count}/{Threshold} verified samples since {Since}", 
                newSamplesCount, threshold, since?.ToString() ?? "Project Start");

            if (newSamplesCount >= threshold)
            {
                bool isRunning = await _unitOfWork.Repository<RetrainHistory, Guid>()
                    .GetQueryable(tracked: false)
                    .AsNoTracking()
                    .AnyAsync(x => x.PipelineType == RetrainPipelineType.WoundClassification
                                   && (x.Status == RetrainStatus.Pending || 
                                       x.Status == RetrainStatus.Training || 
                                       x.Status == RetrainStatus.ExportingData));

                if (!isRunning)
                {
                    _logger.LogWarning("AUTO-RETRAIN: Wound threshold reached! Enqueueing pipeline...");
                    _backgroundJobs.Enqueue<IRetrainOrchestrationService>(s => s.TriggerWoundRetrainAsync(since));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check or trigger wound auto-retrain pipeline.");
        }
    }

    // First Aid Protocol

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