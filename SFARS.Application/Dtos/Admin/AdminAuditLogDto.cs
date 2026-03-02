using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Admin
{
    /// <summary>
    /// DTO for Admin Audit Log entries returned to the client.
    /// </summary>
    public class AdminAuditLogDto
    {
        public Guid Id { get; set; }

        /// <summary>Admin who performed the action.</summary>
        public Guid AdminId { get; set; }

        /// <summary>Admin display name (resolved from navigation).</summary>
        public string? AdminName { get; set; }

        /// <summary>Action performed.</summary>
        public AdminAction Action { get; set; }

        /// <summary>Human-readable action label.</summary>
        public string? ActionDisplay { get; set; }

        /// <summary>Target entity type (e.g. "User").</summary>
        public string EntityType { get; set; } = null!;

        /// <summary>Target entity ID.</summary>
        public Guid EntityId { get; set; }

        /// <summary>Value before the change (JSON).</summary>
        public string? OldValue { get; set; }

        /// <summary>Value after the change (JSON).</summary>
        public string? NewValue { get; set; }

        /// <summary>Reason for the action.</summary>
        public string? Reason { get; set; }

        /// <summary>IP address of admin.</summary>
        public string? IpAddress { get; set; }

        /// <summary>When the action was performed (UTC).</summary>
        public DateTime CreatedAt { get; set; }
    }
}
