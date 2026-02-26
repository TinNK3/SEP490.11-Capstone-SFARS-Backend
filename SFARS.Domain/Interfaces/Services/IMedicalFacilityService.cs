using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services;

public interface IMedicalFacilityService
{
    /// <summary>
    /// Find active medical facilities within a given radius, sorted by distance.
    /// </summary>
    Task<IServiceResult> GetNearbyFacilitiesAsync(
        double latitude, double longitude, double radiusKm = 10, int limit = 20);
}