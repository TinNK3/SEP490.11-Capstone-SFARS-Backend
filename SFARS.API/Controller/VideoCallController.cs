using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Models.VideoCall;

namespace SFARS.API.Controller
{
    /// <summary>
    /// Video Call management endpoints
    /// </summary>
    [ApiController]
    public class VideoCallController : ControllerBase
    {
        private readonly IVideoCallService _videoCallService;

        public VideoCallController(IVideoCallService videoCallService)
        {
            _videoCallService = videoCallService;
        }

        /// <summary>
        /// Request a join token for an Agora video call channel.
        /// Incident participants only.
        /// </summary>
        /// <param name="incidentId">Incident ID</param>
        [Authorize]
        [HttpPost(APIRoute.VideoCall.GetToken, Name = nameof(GetJoinTokenAsync))]
        public async Task<IActionResult> GetJoinTokenAsync([FromRoute] Guid incidentId)
        {
            var userId = User.GetUserId();
            var result = await _videoCallService.GetJoinTokenAsync(incidentId, userId);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Initiate a call invitation to the other party.
        /// Sends FCM and prepares SignalR signaling.
        /// </summary>
        /// <param name="incidentId">Incident ID</param>
        [Authorize]
        [HttpPost(APIRoute.VideoCall.Initiate, Name = nameof(InitiateCallAsync))]
        public async Task<IActionResult> InitiateCallAsync([FromRoute] Guid incidentId)
        {
            var userId = User.GetUserId();
            var result = await _videoCallService.InitiateCallAsync(incidentId, userId);
            return this.ToIActionResult(result);
        }
    }
}