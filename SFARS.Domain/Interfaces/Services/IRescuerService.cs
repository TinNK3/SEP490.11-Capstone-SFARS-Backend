using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Interfaces.Services;

public interface IRescuerService
{
    /// <summary>
    /// Get the authenticated rescuer's full profile (User info + RescuerProfile fields).
    /// </summary>
    Task<IServiceResult> GetRescuerProfileAsync(Guid userId);

    /// <summary>
    /// Update all fields of the authenticated rescuer's profile.
    /// </summary>
    Task<IServiceResult> UpdateRescuerProfileAsync<TDto>(Guid userId, TDto dto) where TDto : class;

    /// <summary>
    /// Get a paginated list of all verified rescuers with public metrics.
    /// Used for public directory without requiring authentication.
    /// </summary>
    Task<IServiceResult> GetAllRescuersPublicAsync(PublicRescuerSpecParams specParams);
}