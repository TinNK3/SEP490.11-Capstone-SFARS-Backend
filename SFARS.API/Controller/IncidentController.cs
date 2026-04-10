using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Incident;
using SFARS.Application.Dtos.AiInference;
using SFARS.Application.Dtos.AiReview;
using SFARS.Application.Dtos.Incident;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Specifications.Params;

namespace SFARS.API.Controller
{
    /// <summary>
    /// Incident (SOS) endpoints
    /// </summary>
    [ApiController]
    public class IncidentController : ControllerBase
    {
        private readonly IIncidentService<IncidentDto> _incidentService;

        public IncidentController(IIncidentService<IncidentDto> incidentService)
        {
            _incidentService = incidentService;
        }

        /// <summary>
        /// Create a new incident (SOS report)
        /// </summary>
        /// <param name="req">Incident details</param>
        /// <returns>Created incident</returns>
        [Authorize]
        [HttpPost(APIRoute.Incident.Create, Name = nameof(CreateIncidentAsync))]
        public async Task<IActionResult> CreateIncidentAsync([FromBody] CreateIncidentRequest req)
        {
            var userId = User.GetUserId();
            var result = await _incidentService.CreateIncidentAsync(userId, req.ToIncidentDto());
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Get current user's incidents
        /// </summary>
        /// <param name="specParams">Standard pagination and filter configuration</param>
        /// <returns>Paginated list of incidents</returns>
        [Authorize]
        [HttpGet(APIRoute.Incident.GetMyIncidents, Name = nameof(GetMyIncidentsAsync))]
        public async Task<IActionResult> GetMyIncidentsAsync([FromQuery] IncidentSpecParams specParams)
        {
            var userId = User.GetUserId();
            var result = await _incidentService.GetMyIncidentsAsync(userId, specParams);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Get all incidents for Admin
        /// </summary>
        [Authorize(Roles = UserTypeConstants.Admin)]
        [HttpGet(APIRoute.Incident.GetAllAdmin, Name = nameof(GetAllIncidentsAsync))]
        public async Task<IActionResult> GetAllIncidentsAsync([FromQuery] IncidentSpecParams specParams)
        {
            var result = await _incidentService.GetAllIncidentsAsync(specParams);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Get status history for a specific incident for Admin
        /// </summary>
        [Authorize(Roles = UserTypeConstants.Admin)]
        [HttpGet(APIRoute.Incident.GetStatusHistoryAdmin, Name = nameof(GetIncidentStatusHistoryAsync))]
        public async Task<IActionResult> GetIncidentStatusHistoryAsync([FromRoute] Guid id)
        {
            var result = await _incidentService.GetStatusHistoryAsync(id);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Get recent incidents for community tracking (Anonymous, with privacy masking)
        /// </summary>
        [AllowAnonymous]
        [HttpGet(APIRoute.Incident.GetRecentCommunity, Name = nameof(GetRecentCommunityIncidentsAsync))]
        public async Task<IActionResult> GetRecentCommunityIncidentsAsync([FromQuery] BaseSpecParams specParams)
        {
            var result = await _incidentService.GetRecentCommunityIncidentsAsync(specParams);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Get incident by ID
        /// </summary>
        /// <param name="id">Incident ID</param>
        /// <returns>Incident details</returns>
        [Authorize]
        [HttpGet(APIRoute.Incident.GetById, Name = nameof(GetIncidentByIdAsync))]
        public async Task<IActionResult> GetIncidentByIdAsync([FromRoute] Guid id)
        {
            var userId = User.GetUserId();
            var result = await _incidentService.GetIncidentByIdAsync(userId, id);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Upload media and run AI snake detection in a single flow.
        /// Combines: upload to cloud → YOLO inference → DB first-aid → save all in 1 transaction.
        /// </summary>
        /// <param name="id">Incident ID</param>
        /// <param name="req">Photo file and media type</param>
        /// <returns>AI analysis with snake detection, first aid steps, and prohibitions</returns>
        [Authorize]
        [HttpPost(APIRoute.Incident.Analyze, Name = nameof(AnalyzeAsync))]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
        public async Task<IActionResult> AnalyzeAsync(
            [FromRoute] Guid id,
            [FromForm] AnalyzeIncidentRequest req)
        {
            var userId = User.GetUserId();

            var aiInferenceService = HttpContext.RequestServices
                .GetRequiredService<IAiInferenceService>();

            Stream? stream = null;
            if (req.File != null)
            {
                stream = req.File.OpenReadStream();
            }

            var result = await aiInferenceService.AnalyzeAsync(
                userId: userId,
                incidentId: id,
                imageStream: stream,
                fileName: req.File?.FileName,
                contentType: req.File?.ContentType,
                fileSize: req.File?.Length);

            if (stream != null)
            {
                await stream.DisposeAsync();
            }

            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Manually trigger SOS dispatch.
        /// Overrides the AI countdown or reactivates an accidentally cancelled SOS.
        /// </summary>
        /// <param name="id">Incident ID</param>
        /// <returns>Result of manual dispatch</returns>
        [Authorize]
        [HttpPost(APIRoute.Incident.Dispatch, Name = nameof(ManualDispatchAsync))]
        public async Task<IActionResult> ManualDispatchAsync([FromRoute] Guid id)
        {
            var userId = User.GetUserId();
            var result = await _incidentService.ManualDispatchAsync(userId, id);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Submit an AI Review from a Rescuer
        /// </summary>
        /// <param name="id">Incident ID</param>
        /// <param name="req">Review Details</param>
        /// <returns>Result of submission</returns>
        [Authorize]
        [HttpPost(APIRoute.Incident.AiReview, Name = nameof(SubmitAiReviewAsync))]
        public async Task<IActionResult> SubmitAiReviewAsync(
            [FromRoute] Guid id,
            [FromBody] SubmitAiReviewRequestDto req)
        {
            var userId = User.GetUserId();

            var aiReviewService = HttpContext.RequestServices
                .GetRequiredService<IAiReviewService<SubmitAiReviewRequestDto, FirstAidStepDto>>();

            var result = await aiReviewService.SubmitReviewAsync(id, userId, req);

            return this.ToIActionResult(result);
        }


        #region Tracking

        /// <summary>
        /// Get incident tracking data (locations of all participants)
        /// </summary>
        /// <param name="id">Incident ID</param>
        /// <returns>Tracking data with participant locations</returns>
        [Authorize]
        [HttpGet(APIRoute.Incident.Tracking, Name = nameof(GetIncidentTracking))]
        public async Task<IActionResult> GetIncidentTracking([FromRoute] Guid id)
        {
            var userId = User.GetUserId();
            var result = await _incidentService.GetIncidentTrackingAsync(userId, id);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Get incident tracking via public code (QR sharing, no auth required)
        /// </summary>
        /// <param name="code">Tracking code</param>
        /// <returns>Minimized tracking data</returns>
        [AllowAnonymous]
        [HttpGet(APIRoute.Incident.TrackingByCode, Name = nameof(GetPublicTracking))]
        public async Task<IActionResult> GetPublicTracking([FromQuery] string code)
        {
            var result = await _incidentService.GetPublicTrackingAsync(code);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Generate or regenerate a tracking code for sharing (victim only)
        /// </summary>
        /// <param name="id">Incident ID</param>
        /// <returns>Tracking code and expiry</returns>
        [Authorize]
        [HttpPost(APIRoute.Incident.RegenerateTrackingCode, Name = nameof(RegenerateTrackingCode))]
        public async Task<IActionResult> RegenerateTrackingCode([FromRoute] Guid id)
        {
            var userId = User.GetUserId();
            var result = await _incidentService.RegenerateTrackingCodeAsync(userId, id);
            return this.ToIActionResult(result);
        }

        #endregion

        #region Offline Webhook (SMS)

        /// <summary>
        /// Webhook endpoint for DIY SMS Gateway.
        /// Processes offline SOS signals sent via SMS.
        /// </summary>
        /// <param name="req">SMS Payload</param>
        /// <returns>Result of incident creation</returns>
        [AllowAnonymous]
        [HttpPost(APIRoute.Webhook.SmsSOSInbound, Name = nameof(ReceiveSmsWebhook))]
        public async Task<IActionResult> ReceiveSmsWebhook([FromBody] SmsWebhookRequestDto req)
        {
            var result = await _incidentService.ProcessSmsWebhookAsync(req.SenderPhone, req.MessageBody, req.SecretKey);
            return this.ToIActionResult(result);
        }

        #endregion

        #region SOS Grace Period & Anti-Spam

        /// <summary>
        /// Pre-check SOS eligibility before the user triggers the SOS button.
        /// Pure Redis read — no incident is created.
        /// Returns IsEligible and RequiresWarningConfirmation flags.
        /// FE calls this on SOS screen open; caches result for the session.
        /// </summary>
        [Authorize]
        [HttpGet(APIRoute.Incident.SosPreCheck, Name = nameof(GetSosEligibilityAsync))]
        public async Task<IActionResult> GetSosEligibilityAsync()
        {
            var userId = User.GetUserId();
            var result = await _incidentService.GetSosEligibilityAsync(userId);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Cancel a pending SOS incident within the grace period.
        /// Grace period starts when AI analysis is returned to the user.
        /// Recording cancellations feeds the anti-spam guard.
        /// </summary>
        /// <param name="id">Incident ID to cancel</param>
        [Authorize]
        [HttpPatch(APIRoute.Incident.Cancel, Name = nameof(CancelIncidentAsync))]
        public async Task<IActionResult> CancelIncidentAsync([FromRoute] Guid id)
        {
            var userId = User.GetUserId();
            var result = await _incidentService.CancelIncidentAsync(userId, id);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Resolve an unassigned SOS incident (Fallback mode) when the victim resolves it externally (e.g., calling 115).
        /// </summary>
        /// <param name="id">Incident ID</param>
        [Authorize]
        [HttpPost(APIRoute.Incident.ResolveFallback, Name = nameof(ResolveFallbackAsync))]
        public async Task<IActionResult> ResolveFallbackAsync([FromRoute] Guid id)
        {
            var userId = User.GetUserId();
            var result = await _incidentService.ResolveFallbackAsync(userId, id);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Upload a voice symptom (audio note) for the incident.
        /// </summary>
        /// <param name="id">Incident ID</param>
        /// <param name="audioFile">Audio file recording</param>
        [Authorize]
        [HttpPatch(APIRoute.Incident.VoiceSymptom, Name = nameof(UpdateVoiceSymptomAsync))]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(1 * 1024 * 1024)] // 1MB limit for short audio (e.g. 15s)
        public async Task<IActionResult> UpdateVoiceSymptomAsync(
            [FromRoute] Guid id,
            IFormFile audioFile)
        {
            var userId = User.GetUserId();

            using var stream = audioFile?.OpenReadStream() ?? Stream.Null;
            var result = await _incidentService.UpdateVoiceSymptomAsync(
                userId, 
                id, 
                stream, 
                audioFile?.FileName ?? string.Empty, 
                audioFile?.ContentType ?? string.Empty);

            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Update victim's symptoms (Bottom Sheet UI).
        /// First call includes MinutesSinceBite; subsequent calls send symptoms only.
        /// Automatically notifies the assigned rescuer or broadcasts to available rescuers.
        /// </summary>
        /// <param name="id">Incident ID</param>
        /// <param name="req">Symptom update payload</param>
        [Authorize]
        [HttpPatch(APIRoute.Incident.UpdateSymptoms)]
        public async Task<IActionResult> UpdateSymptomsAsync(Guid id, [FromBody] UpdateSymptomRequest request)
        {
            var result = await _incidentService.UpdateSymptomsAsync(User.GetUserId(), id, request.MinutesSinceBite, request.Symptoms);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Retrieves the time-series history of symptom updates for a specific incident.
        /// Only accessible by the victim or assigned rescuers.
        /// </summary>
        [Authorize]
        [HttpGet(APIRoute.Incident.GetSymptomTimeline)]
        public async Task<IActionResult> GetSymptomTimelineAsync(Guid id)
        {
            var result = await _incidentService.GetSymptomTimelineAsync(User.GetUserId(), id);
            return this.ToIActionResult(result);
        }

        #endregion
    }
}