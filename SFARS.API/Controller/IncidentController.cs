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
        /// <param name="page">Page index (0-based)</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>List of incidents</returns>
        [Authorize]
        [HttpGet(APIRoute.Incident.GetMyIncidents, Name = nameof(GetMyIncidentsAsync))]
        public async Task<IActionResult> GetMyIncidentsAsync([FromQuery] int page = 0, [FromQuery] int pageSize = 10)
        {
            var userId = User.GetUserId();
            var result = await _incidentService.GetMyIncidentsAsync(userId, page, pageSize);
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
                fileSize: req.File?.Length,
                mediaType: req.MediaType);

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

        #endregion
    }
}