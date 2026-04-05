using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.Application.Dtos.Mission;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Params;

namespace SFARS.API.Controller
{
    /// <summary>
    /// Mission (Rescue Operations) endpoints
    /// </summary>
    [ApiController]
    public class MissionController : ControllerBase
    {
        private readonly IMissionService _missionService;

        public MissionController(IMissionService missionService)
        {
            _missionService = missionService;
        }

        /// <summary>
        /// Get current rescuer's mission history
        /// </summary>
        [Authorize(Roles = UserTypeConstants.Rescuer)]
        [HttpGet(APIRoute.Mission.GetMyMissions, Name = nameof(GetMyMissionsAsync))]
        public async Task<IActionResult> GetMyMissionsAsync([FromQuery] MissionSpecParams specParams)
        {
            var rescuerId = User.GetUserId();
            var result = await _missionService.GetMyMissionsAsync(rescuerId, specParams);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Accept an SOS dispatch incident
        /// </summary>
        /// <param name="id">Incident ID</param>
        /// <returns>Result of acceptance</returns>
        [Authorize]
        [HttpPost(APIRoute.Mission.Accept, Name = nameof(AcceptMissionAsync))]
        public async Task<IActionResult> AcceptMissionAsync([FromRoute] Guid id)
        {
            var rescuerId = User.GetUserId();
            var result = await _missionService.AcceptMissionAsync(id, rescuerId);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Update the status of an active mission (e.g. EnRoute, Arrived, Closed)
        /// </summary>
        /// <param name="id">Mission ID</param>
        /// <param name="req">New Status</param>
        [Authorize]
        [HttpPatch(APIRoute.Mission.UpdateStatus, Name = nameof(UpdateMissionStatusAsync))]
        public async Task<IActionResult> UpdateMissionStatusAsync([FromRoute] Guid id, [FromBody] UpdateMissionStatusRequestDto req)
        {
            var rescuerId = User.GetUserId();
            var result = await _missionService.UpdateStatusAsync(id, rescuerId, req.NewStatus);
            return this.ToIActionResult(result);
        }
    }
}