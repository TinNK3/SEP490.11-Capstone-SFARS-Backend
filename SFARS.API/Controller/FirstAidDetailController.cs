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
    /// [Public] Get all first aid details (paginated, searchable)
    /// </summary>
    [AllowAnonymous]
    [HttpGet(APIRoute.FirstAidDetail.GetAll, Name = nameof(GetAllFirstAidDetailsAsync))]
    public async Task<IActionResult> GetAllFirstAidDetailsAsync([FromQuery] BaseSpecParams specParams)
    {
        var result = await _service.GetAllAsync(specParams);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Public] Get a single first aid detail by ID
    /// </summary>
    [AllowAnonymous]
    [HttpGet(APIRoute.FirstAidDetail.GetById, Name = nameof(GetFirstAidDetailByIdAsync))]
    public async Task<IActionResult> GetFirstAidDetailByIdAsync(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Create a new first aid detail step
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPost(APIRoute.Admin.CreateFirstAidDetail, Name = nameof(CreateFirstAidDetailAsync))]
    public async Task<IActionResult> CreateFirstAidDetailAsync([FromBody] CreateFirstAidDetailRequest req)
    {
        var dto = req.ToFirstAidDetailDto();
        var result = await _service.CreateAsync(CurrentUserId, dto);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// [Admin] Update an existing first aid detail step
    /// </summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPut(APIRoute.Admin.UpdateFirstAidDetail, Name = nameof(UpdateFirstAidDetailAsync))]
    public async Task<IActionResult> UpdateFirstAidDetailAsync(Guid id, [FromBody] UpdateFirstAidDetailRequest req)
    {
        var dto = req.ToFirstAidDetailDto();
        var result = await _service.UpdateAsync(id, CurrentUserId, dto);
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