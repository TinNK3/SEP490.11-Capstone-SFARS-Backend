using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.Domain.Interfaces.Services;

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
    }
}