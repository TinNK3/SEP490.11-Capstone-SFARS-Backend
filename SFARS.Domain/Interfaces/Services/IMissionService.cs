using SFARS.Domain.Common.Enum;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Interfaces.Services;

public interface IMissionService
{
    Task<IServiceResult> GetMyMissionsAsync(Guid rescuerId, MissionSpecParams specParams);
    Task<IServiceResult> GetTodoMissionsAsync(Guid rescuerId, MissionSpecParams specParams);
    Task<IServiceResult> AcceptMissionAsync(Guid incidentId, Guid rescuerId);
    Task<IServiceResult> UpdateStatusAsync(Guid missionId, Guid rescuerId, IncidentStatus newStatus);
    Task ClaimTimeoutAsync(Guid missionId);
}