using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Incident;
using SFARS.Application.Dtos.Incident;
using SFARS.Domain.Interfaces.Services;

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
        /// <param name="pageIndex">Page index (0-based)</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>List of incidents</returns>
        [Authorize]
        [HttpGet(APIRoute.Incident.GetMyIncidents, Name = nameof(GetMyIncidentsAsync))]
        public async Task<IActionResult> GetMyIncidentsAsync([FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 10)
        {
            var userId = User.GetUserId();
            var result = await _incidentService.GetMyIncidentsAsync(userId, pageIndex, pageSize);
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
        /// Upload media (photo/video) for an incident
        /// </summary>
        /// <param name="id">Incident ID</param>
        /// <param name="req">Media file and type</param>
        /// <returns>Created media record</returns>
        [Authorize]
        [HttpPost(APIRoute.Incident.UploadMedia, Name = nameof(UploadMediaAsync))]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
        public async Task<IActionResult> UploadMediaAsync(
            [FromRoute] Guid id,
            [FromForm] UploadIncidentMediaRequest req)
        {
            var userId = User.GetUserId();

            await using var stream = req.File.OpenReadStream();

            var result = await _incidentService.UploadMediaAsync(
                userId: userId,
                incidentId: id,
                stream: stream,
                fileName: req.File.FileName,
                contentType: req.File.ContentType,
                fileSize: req.File.Length,
                mediaType: req.MediaType);
                
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Create AI inference for incident media (snake detection + first aid)
        /// </summary>
        /// <param name="id">Incident ID</param>
        /// <param name="req">Media to analyze</param>
        /// <returns>AI analysis with snake detection and first aid steps</returns>
        [Authorize]
        [HttpPost(APIRoute.Incident.CreateAiInference, Name = nameof(CreateAiInferenceAsync))]
        public async Task<IActionResult> CreateAiInferenceAsync(
            [FromRoute] Guid id,
            [FromBody] CreateAiInferenceRequest req)
        {
            var userId = User.GetUserId();
            
            var aiInferenceService = HttpContext.RequestServices
                .GetRequiredService<IAiInferenceService>();
            
            var result = await aiInferenceService.CreateInferenceAsync(
                userId: userId,
                incidentId: id,
                incidentMediaId: req.IncidentMediaId);
                
            return this.ToIActionResult(result);
        }
    }
}