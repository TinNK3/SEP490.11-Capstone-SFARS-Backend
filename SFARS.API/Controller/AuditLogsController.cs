using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Params;

namespace SFARS.API.Controller
{
    /// <summary>
    /// Admin — Audit Logs (api/admin/audit-logs)
    /// </summary>
    [ApiController]
    [Authorize(Roles = UserTypeConstants.Admin)]
    public class AuditLogsController : ControllerBase
    {
        private readonly IAdminAuditLogService _auditLogService;

        public AuditLogsController(IAdminAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        /// <summary>
        /// [Admin] Get paginated audit logs with optional filters.
        /// </summary>
        /// <remarks>
        /// **Filters (all optional, combinable):**
        /// - `adminId`    — filter by the admin who performed the action
        /// - `action`     — enum: `createUser` | `updateUserStatus`
        /// - `entityType` — filter by target type (e.g. `User`)
        /// - `entityId`   — filter by target entity ID
        /// - `search`     — full-text search on reason / admin name / email
        /// - `dateRange[0]` / `dateRange[1]` — date range (ISO 8601)
        ///
        /// **Sorting** (`sort` query param):
        /// - `CreatedAt` / `-CreatedAt` (prefix `-` = descending, default: `-CreatedAt`)
        /// - `Action`, `EntityType`, etc.
        ///
        /// **Pagination:**
        /// - `page` (1-based, default: 1)
        /// - `limit` (default: 10)
        /// </remarks>
        [HttpGet(APIRoute.Admin.GetAuditLogs, Name = nameof(GetAuditLogsAsync))]
        public async Task<IActionResult> GetAuditLogsAsync([FromQuery] AdminAuditLogSpecParams specParams)
        {
            var result = await _auditLogService.GetLogsAsync(specParams);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Get audit logs for a specific user.
        /// </summary>
        [HttpGet(APIRoute.Admin.GetUserAuditLogs, Name = nameof(GetUserAuditLogsAsync))]
        public async Task<IActionResult> GetUserAuditLogsAsync(
            Guid id,
            [FromQuery] BaseSpecParams specParams)
        {
            var result = await _auditLogService.GetLogsByEntityAsync("User", id, specParams);
            return this.ToIActionResult(result);
        }
    }
}
