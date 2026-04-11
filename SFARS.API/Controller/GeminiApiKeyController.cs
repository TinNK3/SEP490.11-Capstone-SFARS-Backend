using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Admin;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Params;

namespace SFARS.API.Controller;

/// <summary>
/// Admin management of Gemini API Keys
/// </summary>
[ApiController]
[Authorize(Roles = UserTypeConstants.Admin)]
public class GeminiApiKeyController : ControllerBase
{
    private readonly IGeminiApiKeyService _geminiKeyService;

    public GeminiApiKeyController(IGeminiApiKeyService geminiKeyService)
    {
        _geminiKeyService = geminiKeyService;
    }

    private Guid CurrentUserId => User.GetUserId();

    /// <summary>
    /// [Admin] Get all Gemini API keys (paginated). Key values are masked.
    /// </summary>
    [HttpGet(APIRoute.Admin.GetAllGeminiKeys, Name = nameof(GetAllKeysPaginatedAsync))]
    public async Task<IActionResult> GetAllKeysPaginatedAsync([FromQuery] BaseSpecParams specParams)
    {
        var result = await _geminiKeyService.GetAllKeysAsync(specParams);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Add a new Gemini API key
    /// </summary>
    [HttpPost(APIRoute.Admin.CreateGeminiKey, Name = nameof(CreateKeyAsync))]
    public async Task<IActionResult> CreateKeyAsync([FromBody] CreateGeminiKeyRequest req)
    {
        var result = await _geminiKeyService.AddKeyAsync(req.KeyValue, req.Label, CurrentUserId);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Toggle a Gemini API key (Invert Enable/Disable state)
    /// </summary>
    [HttpPatch(APIRoute.Admin.ToggleGeminiKey, Name = nameof(ToggleKeyAsync))]
    public async Task<IActionResult> ToggleKeyAsync(Guid id)
    {
        var result = await _geminiKeyService.ToggleKeyAsync(id, CurrentUserId);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Delete a Gemini API key
    /// </summary>
    [HttpDelete(APIRoute.Admin.DeleteGeminiKey, Name = nameof(DeleteKeyAsync))]
    public async Task<IActionResult> DeleteKeyAsync(Guid id)
    {
        var result = await _geminiKeyService.DeleteKeyAsync(id, CurrentUserId);
        return this.ToIActionResult(result);
    }
}