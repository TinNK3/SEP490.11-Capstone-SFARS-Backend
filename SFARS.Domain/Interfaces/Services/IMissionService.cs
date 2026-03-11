using SFARS.Domain.Common.Enum;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services;

public interface IMissionService
{
    Task<IServiceResult> AcceptMissionAsync(Guid incidentId, Guid rescuerId);
    Task<IServiceResult> UpdateStatusAsync(Guid missionId, Guid rescuerId, IncidentStatus newStatus);
    Task ClaimTimeoutAsync(Guid missionId);
}