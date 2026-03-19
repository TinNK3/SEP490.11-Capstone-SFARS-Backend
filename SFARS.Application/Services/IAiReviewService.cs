using SFARS.Application.Dtos.AiInference;
using SFARS.Application.Dtos.AiReview;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Application.Services;

public interface IAiReviewService
{
    Task<IServiceResult> SubmitReviewAsync(Guid incidentId, Guid rescuerId, SubmitAiReviewRequestDto request);
    
    /// <summary>
    /// Gets the effective first aid protocol for an incident, adhering to the AI review rules.
    /// Unreviewed or Unassessable -> Unknown Toxin + General Prohibitions
    /// ConfirmedCorrect / Corrected -> Specific Toxin
    /// </summary>
    Task<(List<FirstAidStepDto> steps, List<string> prohibitions)> GetEffectiveFirstAidProtocolAsync(Incident incident);
}