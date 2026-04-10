using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Models.VideoCall;

namespace SFARS.Domain.Interfaces.Services;

public interface IVideoCallService
{
    /// <summary>
    /// Generates a join token for a specific incident.
    /// Validates that the requesting user is either the victim or the assigned rescuer.
    /// </summary>
    Task<IServiceResult> GetJoinTokenAsync(Guid incidentId, Guid userId);

    /// <summary>
    /// Sends a call invitation signal via SignalR and FCM (for offline users).
    /// </summary>
    Task<IServiceResult> InitiateCallAsync(Guid incidentId, Guid callerId);
}