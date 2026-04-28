using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Params;
using SFARS.Infrastructure.Configurations;

namespace SFARS.API.Controller.Admin
{
    /// <summary>
    /// Admin — MLOps &amp; AI Retraining (api/admin/mlops)
    /// </summary>
    [ApiController]
    [Authorize(Roles = UserTypeConstants.Admin)]
    public class MlopsController : ControllerBase
    {
        private readonly IDataSetExportService _exportService;
        private readonly IRetrainOrchestrationService _retrainService;
        private readonly IBackgroundJobClient _backgroundJobs;
        private readonly MlopsOptions _mlopsOptions;

        public MlopsController(
            IDataSetExportService exportService,
            IRetrainOrchestrationService retrainService,
            IBackgroundJobClient backgroundJobs,
            IOptions<MlopsOptions> mlopsOptions)
        {
            _exportService = exportService;
            _retrainService = retrainService;
            _backgroundJobs = backgroundJobs;
            _mlopsOptions = mlopsOptions.Value;
        }

        // Snake Species Pipeline

        /// <summary>
        /// [Admin] Export human-verified snake training data for AI retraining.
        /// </summary>
        [HttpGet(APIRoute.AdminMlops.ExportData, Name = nameof(ExportDataAsync))]
        public async Task<IActionResult> ExportDataAsync([FromQuery] BaseSpecParams specParams)
        {
            var result = await _exportService.ExportTrainingDataAsync(specParams);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Machine-to-Machine] Snake pipeline fetches data using API Key instead of JWT.
        /// </summary>
        [AllowAnonymous]
        [HttpGet(APIRoute.AdminMlops.ExportByKey, Name = nameof(ExportDataByApiKeyAsync))]
        public async Task<IActionResult> ExportDataByApiKeyAsync(
            [FromHeader(Name = "X-Api-Key")] string apiKey,
            [FromQuery] BaseSpecParams specParams)
        {
            if (string.IsNullOrEmpty(_mlopsOptions.ApiKey) || apiKey != _mlopsOptions.ApiKey)
                return Unauthorized(new { message = "Invalid API Key" });

            var result = await _exportService.ExportTrainingDataAsync(specParams);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Get the most recent retrain history records.
        /// </summary>
        [HttpGet(APIRoute.AdminMlops.GetRetrainHistory, Name = nameof(GetRetrainHistoryAsync))]
        public async Task<IActionResult> GetRetrainHistoryAsync([FromQuery] int count = 10)
        {
            var result = await _retrainService.GetRetrainHistoryAsync(count);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Manually trigger the snake species MLOps retrain pipeline.
        /// </summary>
        [HttpPost(APIRoute.AdminMlops.TriggerRetrain, Name = nameof(TriggerRetrainAsync))]
        public IActionResult TriggerRetrainAsync()
        {
            _backgroundJobs.Enqueue<IRetrainOrchestrationService>(s => s.TriggerRetrainAsync(null));
            return Accepted(new { message = "Snake species retrain pipeline has been queued." });
        }

        /// <summary>
        /// [Admin] Exclude one or more images from being used in the MLOps retrain pipelines.
        /// </summary>
        [HttpPost(APIRoute.AdminMlops.ExcludeImages, Name = nameof(ExcludeImagesFromRetrainAsync))]
        public async Task<IActionResult> ExcludeImagesFromRetrainAsync([FromBody] List<Guid> inferenceIds)
        {
            var result = await _exportService.ExcludeImagesFromRetrainAsync(inferenceIds);
            return this.ToIActionResult(result);
        }

        // Wound Classification Pipeline

        /// <summary>
        /// [Admin] Export human-verified wound training data for AI retraining.
        /// </summary>
        [HttpGet(APIRoute.AdminMlops.ExportWoundData, Name = nameof(ExportWoundDataAsync))]
        public async Task<IActionResult> ExportWoundDataAsync([FromQuery] BaseSpecParams specParams)
        {
            var result = await _exportService.ExportWoundTrainingDataAsync(specParams);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Machine-to-Machine] Wound pipeline fetches data using API Key instead of JWT.
        /// </summary>
        [AllowAnonymous]
        [HttpGet(APIRoute.AdminMlops.ExportWoundByKey, Name = nameof(ExportWoundDataByApiKeyAsync))]
        public async Task<IActionResult> ExportWoundDataByApiKeyAsync(
            [FromHeader(Name = "X-Api-Key")] string apiKey,
            [FromQuery] BaseSpecParams specParams)
        {
            if (string.IsNullOrEmpty(_mlopsOptions.ApiKey) || apiKey != _mlopsOptions.ApiKey)
                return Unauthorized(new { message = "Invalid API Key" });

            var result = await _exportService.ExportWoundTrainingDataAsync(specParams);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Manually trigger the wound classification MLOps retrain pipeline.
        /// </summary>
        [HttpPost(APIRoute.AdminMlops.TriggerWoundRetrain, Name = nameof(TriggerWoundRetrainAsync))]
        public IActionResult TriggerWoundRetrainAsync()
        {
            _backgroundJobs.Enqueue<IRetrainOrchestrationService>(s => s.TriggerWoundRetrainAsync(null));
            return Accepted(new { message = "Wound classification retrain pipeline has been queued." });
        }
    }
}