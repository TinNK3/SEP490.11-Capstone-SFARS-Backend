using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.Application.Dtos.Facility;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.API.Controller;

/// <summary>
/// Medical facility endpoints
/// </summary>
[ApiController]
public class FacilityController : ControllerBase
{
    private readonly IMedicalFacilityService<FacilityDto> _facilityService;

    public FacilityController(
        IMedicalFacilityService<FacilityDto> facilityService)
    {
        _facilityService = facilityService;
    }

    /// <summary>
    /// Find nearby medical facilities sorted by distance.
    /// Requires latitude and longitude query parameters.
    /// </summary>
    /// <param name="lat">Required: User latitude</param>
    /// <param name="lng">Required: User longitude</param>
    /// <param name="radiusKm">Search radius in kilometers (default: 10)</param>
    /// <param name="limit">Max results (default: 20)</param>
    /// <returns>List of nearby facilities with distances</returns>
    [Authorize]
    [HttpGet(APIRoute.Facility.Nearby, Name = nameof(GetNearbyFacilities))]
    public async Task<IActionResult> GetNearbyFacilities(
        [FromQuery] double? lat,
        [FromQuery] double? lng,
        [FromQuery] double radiusKm = 10,
        [FromQuery] int limit = 20)
    {
        var userId = User.GetUserId();
        var result = await _facilityService.GetNearbyFacilitiesAsync(userId, lat, lng, radiusKm, limit);
        return this.ToIActionResult(result);
    }
}
