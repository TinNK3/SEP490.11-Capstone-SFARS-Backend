using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Snake;
using SFARS.Application.Dtos;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Specifications.Params;

namespace SFARS.API.Controller;

[ApiController]
public class SnakeController : ControllerBase
{
    private readonly ISnakeService<SnakeDto> _snakeService;

    public SnakeController(ISnakeService<SnakeDto> snakeService)
    {
        _snakeService = snakeService;
    }

    #region Public Read Endpoints

    /// <summary>
    /// Standalone API to identify a snake species from an uploaded image.
    /// This is independent of the SOS workflow.
    /// </summary>
    [HttpPost(APIRoute.Snake.IdentifySpecies, Name = nameof(IdentifySnakeSpeciesAsync))]
    [AllowAnonymous]
    public async Task<IActionResult> IdentifySnakeSpeciesAsync(
        [FromServices] IAiInferenceService aiService,
        [FromForm] IdentifySnakeRequest request)
    {
        var result = await aiService.IdentifySnakeAsync(
            request.Image.OpenReadStream(), 
            request.Image.ContentType, 
            request.Image.Length);
            
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// Retrieves all snake entities asynchronously with optional searching and filtering.
    /// </summary>
    [HttpGet(APIRoute.Snake.GetAll, Name = nameof(GetAllAsync))]
    public async Task<IActionResult> GetAllAsync([FromQuery] SnakeSpecParams specParams)
    {
        return Ok(await _snakeService.GetAllSnakesAsync(specParams));
    }

    /// <summary>
    /// Retrieves a snake by its ID
    /// </summary>
    [HttpGet(APIRoute.Snake.GetById, Name = nameof(GetByIdAsync))]
    public async Task<IActionResult> GetByIdAsync(Guid id)
    {
        return Ok(await _snakeService.GetSnakeById(id));
    }

    #endregion

    #region Admin Write Endpoints

    /// <summary>
    /// Creates a new snake entity asynchronously. (Admin only)
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPost(APIRoute.Snake.Create, Name = nameof(CreateAsync))]
    public async Task<IActionResult> CreateAsync([FromBody] CreateSnakeRequest req)
    {
        return Ok(await _snakeService.CreateSnake(req.ToSnake()));
    }

    /// <summary>
    /// Updates an existing snake with field-level audit logging. (Admin only)
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPut(APIRoute.Snake.Update, Name = nameof(UpdateAsync))]
    public async Task<IActionResult> UpdateAsync([FromRoute] Guid id, [FromBody] UpdateSnakeRequest req)
    {
        return Ok(await _snakeService.UpdateSnakeAsync(id, req.ToSnakeForUpdate(), req.ChangeReason));
    }

    /// <summary>
    /// Soft-deletes a snake entity asynchronously. (Admin only)
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpDelete(APIRoute.Snake.Delete, Name = nameof(DeleteAsync))]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        return Ok(await _snakeService.DeleteSnake(id));
    }

    #endregion

    #region Import Endpoints (Admin only)

    /// <summary>
    /// Preview Excel (.xlsx) import: parse and validate, returns summary without writing to DB.
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPost(APIRoute.Snake.ImportPreview, Name = nameof(ImportPreviewAsync))]
    public async Task<IActionResult> ImportPreviewAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Excel (.xlsx) file is required");

        using var stream = file.OpenReadStream();
        return Ok(await _snakeService.PreviewImportAsync(stream));
    }

    /// <summary>
    /// Apply Excel (.xlsx) import: upsert by ScientificName with full audit trail.
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPost(APIRoute.Snake.ImportApply, Name = nameof(ImportApplyAsync))]
    public async Task<IActionResult> ImportApplyAsync(IFormFile file, [FromQuery] string? changeReason)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Excel (.xlsx) file is required");

        using var stream = file.OpenReadStream();
        return Ok(await _snakeService.ApplyImportAsync(stream, changeReason));
    }

    #endregion

    #region History & Revert (Admin only)

    /// <summary>
    /// Get change history for a specific snake with pagination.
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpGet(APIRoute.Snake.History, Name = nameof(GetHistoryAsync))]
    public async Task<IActionResult> GetHistoryAsync(Guid id, [FromQuery] BaseSpecParams specParams)
    {
        return Ok(await _snakeService.GetSnakeChangeHistory(id, specParams));
    }

    /// <summary>
    /// Revert a specific field change using its ChangeLog entry ID.
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPut(APIRoute.Snake.Revert, Name = nameof(RevertFieldAsync))]
    public async Task<IActionResult> RevertFieldAsync(Guid changeLogId)
    {
        return Ok(await _snakeService.RevertSnakeField(changeLogId));
    }

    #endregion
}