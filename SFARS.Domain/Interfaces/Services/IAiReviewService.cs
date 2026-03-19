using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services;

public interface IAiReviewService<TSubmitRequest, TFirstAidStep>
{
    Task<IServiceResult> SubmitReviewAsync(Guid incidentId, Guid rescuerId, TSubmitRequest request);
    
    /// <summary>
    /// Gets the effective first aid protocol for an incident, adhering to the AI review rules.
    /// Unreviewed or Unassessable -> Unknown Toxin + General Prohibitions
    /// ConfirmedCorrect / Corrected -> Specific Toxin
    /// </summary>
    Task<(List<TFirstAidStep> steps, List<string> prohibitions)> GetEffectiveFirstAidProtocolAsync(Incident incident);
}