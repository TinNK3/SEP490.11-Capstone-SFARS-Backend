using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Facility;
using SFARS.Application.Dtos.Facility;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Params;

namespace SFARS.API.Controller.Admin
{
    /// <summary>
    /// Admin — Treatment Facility Management (api/admin/facilities)
    /// </summary>
    [ApiController]
    [Authorize(Roles = UserTypeConstants.Admin)]
    public class FacilitiesController : ControllerBase
    {
        private readonly IMedicalFacilityService<FacilityDto> _facilityService;

        public FacilitiesController(IMedicalFacilityService<FacilityDto> facilityService)
        {
            _facilityService = facilityService;
        }

        /// <summary>
        /// [Admin] Get paginated facility list with optional filters.
        /// </summary>
        /// <remarks>
        /// **Filters (all optional, combinable):**
        /// - `search` — text search on name / address
        /// - `type` — enum: `Hospital` | `Clinic` | `HealthCenter` | `Pharmacy`
        /// - `isActive` — boolean filter
        /// - `hasAntivenom` — boolean filter
        ///
        /// **Sorting** (`sort` query param):
        /// - `name` / `-name` (prefix `-` = descending)
        /// - `type`, `createdAt`
        ///
        /// **Pagination:**
        /// - `pageIndex` (1-based, default: 1)
        /// - `pageSize` (default: all)
        /// </remarks>
        [HttpGet(APIRoute.AdminFacility.GetAll, Name = nameof(GetAllFacilitiesAsync))]
        public async Task<IActionResult> GetAllFacilitiesAsync(
            [FromQuery] FacilitySpecParams specParams)
        {
            var result = await _facilityService.GetAllFacilitiesAsync(specParams);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Get a single facility by ID.
        /// </summary>
        [HttpGet(APIRoute.AdminFacility.GetById, Name = nameof(GetFacilityByIdAsync))]
        public async Task<IActionResult> GetFacilityByIdAsync(Guid id)
        {
            var result = await _facilityService.GetFacilityByIdAsync(id);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Create a new medical facility with GPS coordinates.
        /// </summary>
        [HttpPost(APIRoute.AdminFacility.Create, Name = nameof(CreateFacilityAsync))]
        public async Task<IActionResult> CreateFacilityAsync([FromBody] CreateFacilityRequest request)
        {
            var result = await _facilityService.CreateFacilityAsync(request.ToFacilityDto());
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Update an existing medical facility.
        /// </summary>
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
        [HttpPut(APIRoute.AdminFacility.Activate, Name = nameof(ActivateFacilityAsync))]
        public async Task<IActionResult> ActivateFacilityAsync(Guid id)
        {
            var result = await _facilityService.ActivateFacilityAsync(id);
            return this.ToIActionResult(result);
        }
    }
}
