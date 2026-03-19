using SFARS.Domain.Common.Enum;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Interfaces.Services
{
    /// <summary>
    /// Service interface for Admin Audit Logging.
    /// </summary>
    public interface IAdminAuditLogService
    {
        /// <summary>Record an admin action.</summary>
        Task LogAsync(
            Guid adminId,
            AdminAction action,
            string entityType,
            Guid entityId,
            string? oldValue,
            string? newValue,
            string? reason = null,
            string? ipAddress = null);

        /// <summary>Get paginated audit logs with optional filters.</summary>
        Task<IServiceResult> GetLogsAsync(AdminAuditLogSpecParams specParams);

        /// <summary>Get audit logs for a specific entity.</summary>
        Task<IServiceResult> GetLogsByEntityAsync(string entityType, Guid entityId, BaseSpecParams specParams);
    }
}
