using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Interfaces.Services;

public interface IMedicalFacilityService<TDto> : IGenericService<MedicalFacility, TDto, Guid>
    where TDto : class
{
    /// <summary>
    /// Find active medical facilities within a given radius, sorted by distance.
    /// Requires latitude and longitude parameters.
    /// </summary>
    /// <param name="userId">User ID (reserved for future use)</param>
    /// <param name="latitude">Required: User latitude</param>
    /// <param name="longitude">Required: User longitude</param>
    /// <param name="radiusKm">Search radius in kilometers</param>
    /// <param name="limit">Max results</param>
    Task<IServiceResult> GetNearbyFacilitiesAsync(
        Guid userId, double? latitude, double? longitude, double radiusKm = 10, int limit = 20);

    /// <summary>
    /// [Admin] Get paginated facility list with filters.
    /// </summary>
    Task<IServiceResult> GetAllFacilitiesAsync(FacilitySpecParams specParams);

    /// <summary>
    /// [Public] Get paginated list of active hospitals.
    /// Does not require authentication.
    /// </summary>
    Task<IServiceResult> GetAllPublicFacilitiesAsync(BaseSpecParams specParams);

    /// <summary>
    /// [Admin] Get facility by ID.
    /// </summary>
    Task<IServiceResult> GetFacilityByIdAsync(Guid id);

    /// <summary>
    /// [Admin] Create a new medical facility.
    /// </summary>
    Task<IServiceResult> CreateFacilityAsync(TDto dto);

    /// <summary>
    /// [Admin] Update an existing medical facility.
    /// </summary>
    Task<IServiceResult> UpdateFacilityAsync(Guid id, TDto dto);

    /// <summary>
    /// [Admin] Update antivenom availability for a facility.
    /// </summary>
    Task<IServiceResult> UpdateAntivenomAsync(Guid id, bool hasAntivenom);

    /// <summary>
    /// [Admin] Deactivate a facility (soft delete).
    /// </summary>
    Task<IServiceResult> DeactivateFacilityAsync(Guid id);

    /// <summary>
    /// [Admin] Reactivate a deactivated facility.
    /// </summary>
    Task<IServiceResult> ActivateFacilityAsync(Guid id);

    /// <summary>
    /// [Admin] Delete a facility permanently (hard delete).
    /// </summary>
    Task<IServiceResult> DeleteFacilityAsync(Guid id);
}
