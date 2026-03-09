using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services;

public interface IMissionService
{
    Task<IServiceResult> AcceptMissionAsync(Guid incidentId, Guid rescuerId);
    Task ClaimTimeoutAsync(Guid missionId);
}