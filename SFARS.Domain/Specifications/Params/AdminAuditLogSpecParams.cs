using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Specifications.Params
{
    /// <summary>
    /// Query parameters for filtering Admin Audit Logs.
    /// </summary>
    public class AdminAuditLogSpecParams : BaseSpecParams
    {
        /// <summary>Filter by the admin who performed the action.</summary>
        public Guid? AdminId { get; set; }

        /// <summary>Filter by action type (CreateUser, UpdateUserStatus, …).</summary>
        public AdminAction? Action { get; set; }

        /// <summary>Filter by target entity type (e.g. "User").</summary>
        public string? EntityType { get; set; }

        /// <summary>Filter by target entity ID.</summary>
        public Guid? EntityId { get; set; }

        /// <summary>Filter by date range on CreatedAt.</summary>
    }
}
