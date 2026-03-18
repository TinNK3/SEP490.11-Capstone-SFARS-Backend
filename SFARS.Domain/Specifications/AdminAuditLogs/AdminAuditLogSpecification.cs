using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Entities;
using SFARS.Domain.Specifications.Params;
using System.Linq.Expressions;

namespace SFARS.Domain.Specifications.AdminAuditLogs
{
    /// <summary>
    /// Specifications for querying AdminAuditLog.
    /// Use static factory methods:
    ///   - <see cref="List"/>  — paginated + filtered list
    ///   - <see cref="Count"/> — count-only (same filters, no pagination)
    ///   - <see cref="ByEntityId"/> — logs for a specific target entity
    /// </summary>
    public class AdminAuditLogSpecification : BaseSpecification<AdminAuditLog>
    {
        private AdminAuditLogSpecification(Expression<Func<AdminAuditLog, bool>> criteria)
            : base(criteria) { }

        // ─────────────────────────────────────────────────────────────
        // Factory: paginated + filtered list
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Paginated, filtered audit log list for the Admin panel.
        /// Includes Admin navigation.
        /// </summary>
        public static AdminAuditLogSpecification List(AdminAuditLogSpecParams p)
        {
            var spec = new AdminAuditLogSpecification(_ => true);

            spec.ApplyInclude(q => q.Include(a => a.Admin));

            ApplyFilters(spec, p);
            spec.ApplySorting(p.Sort);

            spec.ApplyPaging(p.GetTake(), p.GetSkip());
            return spec;
        }

        // ─────────────────────────────────────────────────────────────
        // Factory: count-only
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Count-only variant — same filters, no pagination or sorting.
        /// </summary>
        public static AdminAuditLogSpecification Count(AdminAuditLogSpecParams p)
        {
            var spec = new AdminAuditLogSpecification(_ => true);
            ApplyFilters(spec, p);
            return spec;
        }

        // ─────────────────────────────────────────────────────────────
        // Factory: logs for a specific entity (e.g. User {id})
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Fetches audit logs targeting a specific entity.
        /// </summary>
        public static AdminAuditLogSpecification ByEntityId(
            string entityType, Guid entityId, BaseSpecParams p)
        {
            var spec = new AdminAuditLogSpecification(
                a => a.EntityType == entityType && a.EntityId == entityId);

            spec.ApplyInclude(q => q.Include(a => a.Admin));
            spec.AddOrderByDescending(a => a.CreatedAt);
            spec.ApplyPaging(p.GetTake(), p.GetSkip());
            return spec;
        }

        // ─────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────

        private static void ApplyFilters(AdminAuditLogSpecification spec, AdminAuditLogSpecParams p)
        {
            if (p.AdminId.HasValue)
                spec.AddFilter(a => a.AdminId == p.AdminId.Value);

            if (p.Action.HasValue)
                spec.AddFilter(a => a.Action == p.Action.Value);

            if (!string.IsNullOrEmpty(p.EntityType))
                spec.AddFilter(a => a.EntityType == p.EntityType);

            if (p.EntityId.HasValue)
                spec.AddFilter(a => a.EntityId == p.EntityId.Value);

            ApplySearchFilter(spec, p.Search);

            if (p.DateRange != null && p.DateRange.Length > 1)
            {
                var from = p.DateRange[0];
                var to = p.DateRange[1];
                if (from is null && to.HasValue)
                    spec.AddFilter(a => a.CreatedAt <= to.Value);
                else if (from.HasValue && to is null)
                    spec.AddFilter(a => a.CreatedAt >= from.Value);
                else if (from.HasValue && to.HasValue)
                    spec.AddFilter(a => a.CreatedAt.Date >= from.Value.Date
                                     && a.CreatedAt.Date <= to.Value.Date);
            }
        }

        private void ApplySorting(string? sortBy)
        {
            if (!string.IsNullOrEmpty(sortBy))
            {
                var isDescending = sortBy.StartsWith("-");
                var propertyName = isDescending ? sortBy[1..] : sortBy;
                if (!string.IsNullOrEmpty(propertyName))
                {
                    try
                    {
                        var parameter = Expression.Parameter(typeof(AdminAuditLog), "a");
                        var property = Expression.Property(parameter, propertyName);
                        var expr = Expression.Lambda<Func<AdminAuditLog, object>>(
                            Expression.Convert(property, typeof(object)), parameter);
                        if (isDescending) AddOrderByDescending(expr);
                        else              AddOrderBy(expr);
                        return;
                    }
                    catch (ArgumentException) { /* fallback below */ }
                }
            }

            // Default: newest first
            AddOrderByDescending(a => a.CreatedAt);
        }

        private static void ApplySearchFilter(AdminAuditLogSpecification spec, string? rawSearch)
        {
            var search = rawSearch?.Trim();
            if (string.IsNullOrEmpty(search))
                return;

            var normalized = search.ToLower();
            spec.AddFilter(a =>
                (a.Reason != null && a.Reason.ToLower().Contains(normalized))
                || a.Admin.FirstName.ToLower().Contains(normalized)
                || a.Admin.LastName.ToLower().Contains(normalized)
                || a.Admin.Email.ToLower().Contains(normalized));
        }
    }
}
