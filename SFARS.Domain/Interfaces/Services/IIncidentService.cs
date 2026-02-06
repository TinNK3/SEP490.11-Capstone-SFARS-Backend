using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services
{
    public interface IIncidentService<TDto> : IGenericService<Incident, TDto, Guid>
        where TDto : class
    {
        Task<IServiceResult> CreateIncidentAsync(Guid userId, TDto dto);
        Task<IServiceResult> GetMyIncidentsAsync(Guid userId, int pageIndex = 0, int pageSize = 10);
        Task<IServiceResult> GetIncidentByIdAsync(Guid userId, Guid incidentId);
    }
}