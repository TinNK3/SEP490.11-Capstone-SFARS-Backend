using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Specifications.Params;
using System.Linq.Expressions;

namespace SFARS.Domain.Specifications.Users
{
    /// <summary>
    /// All User-related specifications in one place.
    /// Use the static factory methods to create the correct variant:
    ///   - <see cref="ById"/>  — single user with roles (for GetMe, UpdateMe, auth, etc.)
    ///   - <see cref="List"/>  — paginated + filtered list (for Admin user list)
    /// </summary>
    public class UserSpecification : BaseSpecification<User>
    {
        private UserSpecification(Expression<Func<User, bool>> criteria)
            : base(criteria) { }

        // ─────────────────────────────────────────────────────────────
        // Factory: single user by ID, eager-loads roles
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Fetches a single user by ID, including UserRoles → Role navigation.
        /// </summary>
        public static UserSpecification ById(Guid userId)
        {
            var spec = new UserSpecification(u => u.Id == userId);
            spec.EnableSplitQuery();
            spec.ApplyInclude(q => q
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role));
            return spec;
        }

        // ─────────────────────────────────────────────────────────────
        // Factory: paginated + filtered list (Admin panel)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Paginated, filtered user list for the Admin panel.
        /// Includes roles and rescuer profile; supports search, status, role,
        /// date-range, and dynamic sorting.
        /// </summary>
        public static UserSpecification List(UserSpecParams p)
        {
            var spec = new UserSpecification(BuildSearchCriteria(p.Search));

            spec.EnableSplitQuery();
            spec.ApplyInclude(q => q
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role));
            spec.ApplyInclude(q => q.Include(u => u.RescuerProfile!));

            ApplyCommonFilters(spec, p);

            // Sorting
            if (!string.IsNullOrEmpty(p.Sort))
                spec.ApplySorting(p.Sort.Trim());
            else
                spec.AddOrderByDescending(u => u.CreatedAt);

            spec.ApplyPaging(p.GetTake(), p.GetSkip());
            return spec;
        }

        // ─────────────────────────────────────────────────────────────
        // Factory: count-only (no pagination) — mirrors List filters
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Count-only variant of <see cref="List"/> — same filters but no pagination or sorting.
        /// </summary>
        public static UserSpecification Count(UserSpecParams p)
        {
            var spec = new UserSpecification(BuildSearchCriteria(p.Search));

            spec.ApplyInclude(q => q
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role));

            ApplyCommonFilters(spec, p);

            return spec;
        }

        // ─────────────────────────────────────────────────────────────
        // Factory: Public Rescuers List
        // ─────────────────────────────────────────────────────────────

        public static UserSpecification PublicRescuersList(PublicRescuerSpecParams p)
        {
            var spec = new UserSpecification(BuildSearchCriteria(p.Search));
            
            // Hardcode constraints for public rescuer list
            spec.AddFilter(u => u.Status == UserStatus.Active);
            spec.AddFilter(u => u.UserRoles.Any(ur => ur.Role.RoleName == UserTypeConstants.Rescuer));
            spec.AddFilter(u => u.RescuerProfile!.IsVerified == true); // Only verified profiles

            if (!string.IsNullOrEmpty(p.FirstName))
                spec.AddFilter(u => u.FirstName.Contains(p.FirstName));
            if (!string.IsNullOrEmpty(p.LastName))
                spec.AddFilter(u => u.LastName.Contains(p.LastName));
            if (p.Gender != null)
                spec.AddFilter(u => u.Gender == p.Gender);

            // Sorting
            if (!string.IsNullOrEmpty(p.Sort))
                spec.ApplySorting(p.Sort.Trim());
            else
                spec.AddOrderByDescending(u => u.CreatedAt);

            spec.ApplyPaging(p.GetTake(), p.GetSkip());
            return spec;
        }

        public static UserSpecification PublicRescuersCount(PublicRescuerSpecParams p)
        {
            var spec = new UserSpecification(BuildSearchCriteria(p.Search));
            
            // Hardcode constraints for public rescuer count
            spec.AddFilter(u => u.Status == UserStatus.Active);
            spec.AddFilter(u => u.UserRoles.Any(ur => ur.Role.RoleName == UserTypeConstants.Rescuer));
            spec.AddFilter(u => u.RescuerProfile!.IsVerified == true); // Only verified profiles

            if (!string.IsNullOrEmpty(p.FirstName))
                spec.AddFilter(u => u.FirstName.Contains(p.FirstName));
            if (!string.IsNullOrEmpty(p.LastName))
                spec.AddFilter(u => u.LastName.Contains(p.LastName));
            if (p.Gender != null)
                spec.AddFilter(u => u.Gender == p.Gender);

            return spec;
        }

        // ─────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Apply all common filters to specification (used by both List and Count).
        /// DRY principle: eliminates duplicated filter logic.
        /// </summary>
        private static void ApplyCommonFilters(UserSpecification spec, UserSpecParams p)
        {
            if (!string.IsNullOrEmpty(p.Role))
                spec.AddFilter(u => u.UserRoles.Any(ur => ur.Role.RoleName == p.Role.Trim()));
            if (p.Status != null)
                spec.AddFilter(u => u.Status == p.Status);
            if (!string.IsNullOrEmpty(p.FirstName))
                spec.AddFilter(u => u.FirstName.Contains(p.FirstName));
            if (!string.IsNullOrEmpty(p.LastName))
                spec.AddFilter(u => u.LastName.Contains(p.LastName));
            if (p.Gender != null)
                spec.AddFilter(u => u.Gender == p.Gender);

            if (p.CreatedFrom.HasValue)
                spec.AddFilter(u => u.CreatedAt >= p.CreatedFrom.Value);

            if (p.CreatedTo.HasValue)
            {
                var toDate = p.CreatedTo.Value.Date.AddDays(1).AddTicks(-1);
                spec.AddFilter(u => u.CreatedAt <= toDate);
            }

            if (p.ModifiedFrom.HasValue)
                spec.AddFilter(u => u.UpdatedAt >= p.ModifiedFrom.Value);

            if (p.ModifiedTo.HasValue)
            {
                var toDate = p.ModifiedTo.Value.Date.AddDays(1).AddTicks(-1);
                spec.AddFilter(u => u.UpdatedAt <= toDate);
            }

            if (p.DobFrom.HasValue)
                spec.AddFilter(u => u.Dob >= p.DobFrom.Value);

            if (p.DobTo.HasValue)
            {
                var toDate = p.DobTo.Value.Date.AddDays(1).AddTicks(-1);
                spec.AddFilter(u => u.Dob <= toDate);
            }
        }

        private void ApplySorting(string sortBy)
        {
            var isDescending = sortBy.StartsWith("-");
            var propertyName = isDescending ? sortBy[1..] : sortBy;
            if (string.IsNullOrEmpty(propertyName)) return;

            try
            {
                var parameter = Expression.Parameter(typeof(User), "u");
                var property  = Expression.Property(parameter, propertyName);
                var expr      = Expression.Lambda<Func<User, object>>(
                                    Expression.Convert(property, typeof(object)), parameter);
                if (isDescending) AddOrderByDescending(expr);
                else              AddOrderBy(expr);
            }
            catch (ArgumentException)
            {
                AddOrderByDescending(u => u.CreatedAt); // fallback
            }
        }

        private static Expression<Func<User, bool>> BuildSearchCriteria(string? rawSearch)
        {
            var search = rawSearch?.Trim();
            if (string.IsNullOrEmpty(search))
                return _ => true;

            // ⚠️ PERFORMANCE FIX: Removed .ToLower() to preserve database index usage
            // SQL Server handles case-insensitive comparisons by default via collation
            // Using LOWER() function in WHERE clause causes full table scans

            return u =>
                (!string.IsNullOrEmpty(u.Email) && u.Email.Contains(search))
                || (!string.IsNullOrEmpty(u.Phone) && u.Phone.Contains(search))
                || (!string.IsNullOrEmpty(u.FirstName) && u.FirstName.Contains(search))
                || (!string.IsNullOrEmpty(u.LastName) && u.LastName.Contains(search));

            // ⚠️ REMOVED: (u.FirstName + " " + u.LastName).ToLower().Contains()
            // Concatenation in WHERE breaks index usage and adds CPU overhead
            // If full-name search is critical for business logic:
            //   1. Add computed column FullName to User table with Index
            //   2. Implement Full-Text Search (FTS) in SQL Server
            //   3. Use separate search service (Elasticsearch, etc.)
        }
    }
}
