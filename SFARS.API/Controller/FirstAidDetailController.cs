using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Admin;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Params;

namespace SFARS.API.Controller;

/// <summary>
/// Admin management and public access of First Aid Detail steps
/// </summary>
[ApiController]
public class FirstAidDetailController : ControllerBase
{
    private readonly IFirstAidDetailService _service;

    public FirstAidDetailController(IFirstAidDetailService service)
    {
        _service = service;
    }

    private Guid CurrentUserId => User.GetUserId();

    /// <summary>
    /// [Public] Get all first aid procedures grouped by ToxinGroup
    /// </summary>
    [AllowAnonymous]
    [HttpGet(APIRoute.FirstAid.GetAllProcedures, Name = nameof(GetAllFirstAidsGroupedAsync))]
    public async Task<IActionResult> GetAllFirstAidsGroupedAsync([FromQuery] string? toxinGroup)
    {
        var result = await _service.GetAllGroupedAsync(toxinGroup);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Public] Get a single first aid step by ID (Used by Admin CMS to bind edit form)
    /// </summary>
    [AllowAnonymous]
    [HttpGet(APIRoute.FirstAid.GetProcedureById, Name = nameof(GetFirstAidStepByIdAsync))]
    public async Task<IActionResult> GetFirstAidStepByIdAsync(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Create a new first aid detail step
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPost(APIRoute.Admin.CreateFirstAidDetail, Name = nameof(CreateFirstAidDetailAsync))]
    public async Task<IActionResult> CreateFirstAidDetailAsync([FromForm] UpsertFirstAidDetailRequest req, IFormFile? imageFile)
    {
        var dto = req.ToFirstAidDetailDto();
        
        // Wrap image file into a domain-friendly upload info object
        FirstAidImageUploadInfo? imageInfo = imageFile == null ? null : new FirstAidImageUploadInfo(
            imageFile.OpenReadStream(),
            imageFile.FileName,
            imageFile.ContentType);

        var result = await _service.CreateAsync(CurrentUserId, dto, imageInfo);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Update an existing first aid detail step
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPut(APIRoute.Admin.UpdateFirstAidDetail, Name = nameof(UpdateFirstAidDetailAsync))]
    public async Task<IActionResult> UpdateFirstAidDetailAsync(Guid id, [FromForm] UpsertFirstAidDetailRequest req, IFormFile? imageFile)
    {
        var dto = req.ToFirstAidDetailDto();

        // Wrap image file into a domain-friendly upload info object
        FirstAidImageUploadInfo? imageInfo = imageFile == null ? null : new FirstAidImageUploadInfo(
            imageFile.OpenReadStream(),
            imageFile.FileName,
            imageFile.ContentType);

        var result = await _service.UpdateAsync(id, CurrentUserId, dto, imageInfo);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Delete a first aid detail step
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpDelete(APIRoute.Admin.DeleteFirstAidDetail, Name = nameof(DeleteFirstAidDetailAsync))]
    public async Task<IActionResult> DeleteFirstAidDetailAsync(Guid id)
    {
        var result = await _service.DeleteAsync(id, CurrentUserId);
        return this.ToIActionResult(result);
    }
}