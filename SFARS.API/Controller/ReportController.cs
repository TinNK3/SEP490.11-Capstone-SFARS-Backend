using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Models.Reports;

namespace SFARS.API.Controller;

[ApiController]
[Authorize]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// User gửi báo cáo cho Reel hoặc Community Post.
    /// </summary>
    [HttpPost(APIRoute.Reports.Create)]
    public async Task<IActionResult> CreateReportAsync([FromBody] CreateReportModel request)
    {
        var userId = User.GetUserId();
        var result = await _reportService.CreateReportAsync(userId, request);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// User xem các báo cáo mình đã gửi.
    /// </summary>
    [HttpGet(APIRoute.Reports.GetMyReports)]
    public async Task<IActionResult> GetMyReportsAsync()
    {
        var userId = User.GetUserId();
        var result = await _reportService.GetMyReportsAsync(userId);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// Admin lấy toàn bộ báo cáo.
    /// </summary>
    [HttpGet(APIRoute.Reports.AdminGetAll)]
    [Authorize(Roles = UserTypeConstants.Admin)]
    public async Task<IActionResult> GetAllReportsAsync()
    {
        var result = await _reportService.GetAllReportsAsync();
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// Admin duyệt hoặc từ chối báo cáo.
    /// </summary>
    [HttpPatch(APIRoute.Reports.AdminUpdateStatus)]
    [Authorize(Roles = UserTypeConstants.Admin)]
    public async Task<IActionResult> UpdateReportStatusAsync(Guid id, [FromBody] UpdateReportStatusModel request)
    {
        var adminId = User.GetUserId();
        var result = await _reportService.UpdateReportStatusAsync(adminId, id, request);
        return this.ToIActionResult(result);
    }
}
