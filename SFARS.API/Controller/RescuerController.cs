using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Rescuer;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.API.Controller;

/// <summary>
/// Rescuer profile management endpoints
/// </summary>
[ApiController]
[Authorize(Roles = UserTypeConstants.Rescuer)]
public class RescuerController : ControllerBase
{
    private readonly IRescuerService _rescuerService;

    public RescuerController(IRescuerService rescuerService)
    {
        _rescuerService = rescuerService;
    }

    /// <summary>
    /// Get the authenticated rescuer's full profile information
    /// </summary>
    /// <returns>Combined user info + rescuer profile fields</returns>
    [HttpGet(APIRoute.Rescuer.GetProfile, Name = nameof(GetRescuerProfileAsync))]
    public async Task<IActionResult> GetRescuerProfileAsync()
    {
        var userId = User.GetUserId();

        var result = await _rescuerService.GetRescuerProfileAsync(userId);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// Update the authenticated rescuer's full profile information
    /// </summary>
    /// <remarks>
    /// Updates all editable fields: ExperienceYears, VehicleType, LicensePlate, CoverageRadiusKM, IsAvailable.
    /// Requires the caller to be authenticated and have a RescuerProfile in the system.
    /// </remarks>
    /// <param name="req">All rescuer profile fields to update</param>
    /// <returns>Updated rescuer profile</returns>
    [HttpPut(APIRoute.Rescuer.UpdateProfile, Name = nameof(UpdateRescuerProfileAsync))]
    public async Task<IActionResult> UpdateRescuerProfileAsync([FromBody] UpdateRescuerProfileRequest req)
    {
        var userId = User.GetUserId();

        var result = await _rescuerService.UpdateRescuerProfileAsync(userId, req.ToRescuerProfileDto());
        return this.ToIActionResult(result);
    }
}
