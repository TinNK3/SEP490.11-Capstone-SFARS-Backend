using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
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
using SFARS.Infrastructure.Hubs;

namespace SFARS.Application.Services;

public class AiReviewService : IAiReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemMessageService _msgService;
    private readonly IHubContext<LocationTrackingHub> _locationHub;
    private readonly ILogger<AiReviewService> _logger;

    public AiReviewService(
        IUnitOfWork unitOfWork,
        ISystemMessageService msgService,
        IHubContext<LocationTrackingHub> locationHub,
        ILogger<AiReviewService> logger)
    {
        _unitOfWork = unitOfWork;
        _msgService = msgService;
        _locationHub = locationHub;
        _logger = logger;
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

        if (request.ReviewStatus == AiReviewStatus.Corrected && request.CorrectedToxinGroup == null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));

        if (request.ReviewStatus == AiReviewStatus.UnableToAssess && request.UnableToAssessReasonChoice == null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));

        var oldStatus = review.ReviewStatus;

        // Apply changes
        review.ReviewStatus = request.ReviewStatus;
        review.Comment = request.Comment;
        
        // Only set ReviewedAt explicitly if final state
        if (request.ReviewStatus != AiReviewStatus.Deferred)
            review.ReviewedAt = DateTime.UtcNow;

        review.UpdatedBy = rescuerId;
        review.UpdatedAt = DateTime.UtcNow;

        // Ensure we load the attached inference correctly if needed
        AiInference? inference = null;
        if (request.ReviewStatus == AiReviewStatus.ConfirmedCorrect)
        {
            inference = await _unitOfWork.Repository<AiInference, Guid>().GetByIdAsync(review.AiInferenceId);
        }

        switch (request.ReviewStatus)
        {
            case AiReviewStatus.ConfirmedCorrect:
                if (inference == null)
                    return new ServiceResult(ResultCodeConst.SYS_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));
                
                incident.HumanReviewedToxinGroup = inference!.SelectedToxinGroup;
                incident.HumanReviewedSnakeId = inference!.SelectedSnakeId;
                
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

        incident.CurrentAiReviewStatus = request.ReviewStatus;

        // Re-calculate PriorityLevel based on human-reviewed toxicity
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
            default: // UnableToAssess, Deferred → uncertain, keep vigilant
                incident.PriorityLevel = SeverityLevel.High;
                break;
        }
        
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

        // 3. Realtime Notification to the victim/group
        var notificationPayload = new AiReviewCompletedNotificationDto
        {
            IncidentId = incidentId,
            Message = await _msgService.GetMessageAsync(ResultCodeConst.AiReview_Success0001),
            ReviewStatus = request.ReviewStatus.ToString(),
            EffectiveToxinGroup = incident.HumanReviewedToxinGroup?.ToString() ?? ToxinGroup.Unknown.ToString(),
            RequiresFirstAidRefresh = (request.ReviewStatus == AiReviewStatus.ConfirmedCorrect || request.ReviewStatus == AiReviewStatus.Corrected)
        };
        await _locationHub.Clients
            .GroupExcept(LocationConstants.SignalRGroupPrefix + incidentId, new[] { rescuerId.ToString() })
            .SendAsync(LocationConstants.SignalRAiReviewed, notificationPayload);

        return new ServiceResult(ResultCodeConst.AiReview_Success0001, await _msgService.GetMessageAsync(ResultCodeConst.AiReview_Success0001), request.ReviewStatus.ToString());
    }

    public async Task<(List<FirstAidStepDto> steps, List<string> prohibitions)> GetEffectiveFirstAidProtocolAsync(Incident incident)
    {
        var activeGroup = ToxinGroup.Unknown;

        if (incident.CurrentAiReviewStatus == AiReviewStatus.ConfirmedCorrect ||
            incident.CurrentAiReviewStatus == AiReviewStatus.Corrected)
        {
            activeGroup = incident.HumanReviewedToxinGroup ?? ToxinGroup.Unknown;
        }

        // Fetch guidelines for the effective generic ToxinGroup
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