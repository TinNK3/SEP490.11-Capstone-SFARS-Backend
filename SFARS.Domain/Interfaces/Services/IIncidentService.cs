using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Interfaces.Services
{
    public interface IIncidentService<TDto> : IGenericService<Incident, TDto, Guid>
        where TDto : class
    {
        Task<IServiceResult> CreateIncidentAsync(Guid userId, TDto dto);
        Task<IServiceResult> GetMyIncidentsAsync(Guid userId, IncidentSpecParams specParams);
        Task<IServiceResult> GetAllIncidentsAsync(IncidentSpecParams specParams);
        Task<IServiceResult> GetStatusHistoryAsync(Guid incidentId);
        Task<IServiceResult> GetRecentCommunityIncidentsAsync(BaseSpecParams specParams);
        Task<IServiceResult> GetIncidentByIdAsync(Guid userId, Guid incidentId);
        
        /// <summary>
        /// Upload media (photo/video) for an incident
        /// </summary>
        Task<IServiceResult> UploadMediaAsync(
            Guid userId, 
            Guid incidentId, 
            Stream stream, 
            string fileName, 
            string contentType, 
            long fileSize, 
            MediaType mediaType);

        // Tracking
        Task<IServiceResult> GetIncidentTrackingAsync(Guid userId, Guid incidentId);
        Task<IServiceResult> GetPublicTrackingAsync(string trackingCode);
        Task<IServiceResult> RegenerateTrackingCodeAsync(Guid userId, Guid incidentId);

        // Check spam
        Task<IServiceResult> CancelIncidentAsync(Guid userId, Guid incidentId);
        Task<IServiceResult> ManualDispatchAsync(Guid userId, Guid incidentId);
        Task<IServiceResult> ResolveFallbackAsync(Guid userId, Guid incidentId);
        
        Task<IServiceResult> UpdateVoiceSymptomAsync(
            Guid userId, 
            Guid incidentId, 
            Stream audioStream, 
            string fileName, 
            string contentType);

        Task<IServiceResult> GetSosEligibilityAsync(Guid userId);
    }
}