using SFARS.Domain.Interfaces.Services.Base;

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
}