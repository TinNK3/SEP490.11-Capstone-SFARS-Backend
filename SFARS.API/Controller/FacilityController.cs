using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Facility;
using SFARS.Application.Dtos.Facility;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Params;

namespace SFARS.API.Controller;

/// <summary>
/// Medical facility endpoints (nearby + admin management routes)
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

    /// <summary>
    /// [Admin] Get paginated facility list with optional filters.
    /// </summary>
    /// <remarks>
    /// **Filters (all optional, combinable):**
    /// - `search` — text search on name / address
    /// - `type` — enum: `hospital` | `clinic` | `healthCenter` | `pharmacy`
    /// - `isActive` — boolean filter
    /// - `hasAntivenom` — boolean filter
    ///
    /// **Sorting** (`sort` query param):
    /// - `name` / `-name` (prefix `-` = descending)
    /// - `type`, `createdAt`
    ///
    /// **Pagination:**
    /// - `page` (1-based, default: 1)
    /// - `limit` (default: 10)
    /// </remarks>
    [Authorize]
    [HttpGet(APIRoute.AdminFacility.GetAll, Name = nameof(GetAllFacilitiesAsync))]
    public async Task<IActionResult> GetAllFacilitiesAsync([FromQuery] FacilitySpecParams specParams)
    {
        var result = await _facilityService.GetAllFacilitiesAsync(specParams);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Get a single facility by ID.
    /// </summary>
    [Authorize]
    [HttpGet(APIRoute.AdminFacility.GetById, Name = nameof(GetFacilityByIdAsync))]
    public async Task<IActionResult> GetFacilityByIdAsync(Guid id)
    {
        var result = await _facilityService.GetFacilityByIdAsync(id);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Create a new medical facility with GPS coordinates.
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPost(APIRoute.AdminFacility.Create, Name = nameof(CreateFacilityAsync))]
    public async Task<IActionResult> CreateFacilityAsync([FromBody] CreateFacilityRequest request)
    {
        var result = await _facilityService.CreateFacilityAsync(request.ToFacilityDto());
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Update an existing medical facility.
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPut(APIRoute.AdminFacility.Update, Name = nameof(UpdateFacilityAsync))]
    public async Task<IActionResult> UpdateFacilityAsync(Guid id, [FromBody] UpdateFacilityRequest request)
    {
        var result = await _facilityService.UpdateFacilityAsync(id, request.ToFacilityDto());
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Update antivenom availability for a facility.
    /// Dedicated endpoint for quick antivenom status updates.
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPut(APIRoute.AdminFacility.Antivenom, Name = nameof(UpdateAntivenomAsync))]
    public async Task<IActionResult> UpdateAntivenomAsync(Guid id, [FromBody] UpdateAntivenomRequest request)
    {
        var result = await _facilityService.UpdateAntivenomAsync(id, request.HasAntivenom);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Deactivate a facility (soft delete — IsActive = false).
    /// Facility will no longer appear on patient maps.
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPut(APIRoute.AdminFacility.Deactivate, Name = nameof(DeactivateFacilityAsync))]
    public async Task<IActionResult> DeactivateFacilityAsync(Guid id)
    {
        var result = await _facilityService.DeactivateFacilityAsync(id);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Reactivate a deactivated facility.
    /// Facility will appear on patient maps again.
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPut(APIRoute.AdminFacility.Activate, Name = nameof(ActivateFacilityAsync))]
    public async Task<IActionResult> ActivateFacilityAsync(Guid id)
    {
        var result = await _facilityService.ActivateFacilityAsync(id);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Delete a facility permanently (hard delete).
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpDelete(APIRoute.AdminFacility.Delete, Name = nameof(DeleteFacilityAsync))]
    public async Task<IActionResult> DeleteFacilityAsync(Guid id)
    {
        var result = await _facilityService.DeleteFacilityAsync(id);
        return this.ToIActionResult(result);
    }
}
