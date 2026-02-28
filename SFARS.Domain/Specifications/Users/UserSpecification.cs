using Microsoft.EntityFrameworkCore;
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
        public static UserSpecification List(UserSpecParams p, int pageIndex, int pageSize)
        {
            var spec = new UserSpecification(u =>
                string.IsNullOrEmpty(p.Search) ||
                (
                    (!string.IsNullOrEmpty(u.Email)     && u.Email.Contains(p.Search))     ||
                    (!string.IsNullOrEmpty(u.Phone)     && u.Phone.Contains(p.Search))     ||
                    (!string.IsNullOrEmpty(u.FirstName) && u.FirstName.Contains(p.Search)) ||
                    (!string.IsNullOrEmpty(u.LastName)  && u.LastName.Contains(p.Search))  ||
                    (!string.IsNullOrEmpty(u.FirstName) && !string.IsNullOrEmpty(u.LastName) &&
                     (u.FirstName + " " + u.LastName).Contains(p.Search))
                ));

            spec.EnableSplitQuery();
            spec.ApplyInclude(q => q
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role));
            spec.ApplyInclude(q => q.Include(u => u.RescuerProfile!));

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

            if (p.CreateDateRange != null && p.CreateDateRange.Length > 1)
            {
                var from = p.CreateDateRange[0];
                var to   = p.CreateDateRange[1];
                if (from is null && to.HasValue)
                    spec.AddFilter(u => u.CreatedAt <= to.Value);
                else if (from.HasValue && to is null)
                    spec.AddFilter(u => u.CreatedAt >= from.Value);
                else if (from.HasValue && to.HasValue)
                    spec.AddFilter(u => u.CreatedAt.Date >= from.Value.Date
                                     && u.CreatedAt.Date <= to.Value.Date);
            }

            // Sorting
            if (!string.IsNullOrEmpty(p.Sort))
                spec.ApplySorting(p.Sort.Trim());
            else
                spec.AddOrderByDescending(u => u.CreatedAt);

            spec.ApplyPaging(pageSize, pageIndex * pageSize);
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
            var spec = new UserSpecification(u =>
                string.IsNullOrEmpty(p.Search) ||
                (
                    (!string.IsNullOrEmpty(u.Email)     && u.Email.Contains(p.Search))     ||
                    (!string.IsNullOrEmpty(u.Phone)     && u.Phone.Contains(p.Search))     ||
                    (!string.IsNullOrEmpty(u.FirstName) && u.FirstName.Contains(p.Search)) ||
                    (!string.IsNullOrEmpty(u.LastName)  && u.LastName.Contains(p.Search))  ||
                    (!string.IsNullOrEmpty(u.FirstName) && !string.IsNullOrEmpty(u.LastName) &&
                     (u.FirstName + " " + u.LastName).Contains(p.Search))
                ));

            spec.ApplyInclude(q => q
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role));

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

            if (p.CreateDateRange != null && p.CreateDateRange.Length > 1)
            {
                var from = p.CreateDateRange[0];
                var to   = p.CreateDateRange[1];
                if (from is null && to.HasValue)
                    spec.AddFilter(u => u.CreatedAt <= to.Value);
                else if (from.HasValue && to is null)
                    spec.AddFilter(u => u.CreatedAt >= from.Value);
                else if (from.HasValue && to.HasValue)
                    spec.AddFilter(u => u.CreatedAt.Date >= from.Value.Date
                                     && u.CreatedAt.Date <= to.Value.Date);
            }

            return spec;
        }

        // ─────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────

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
    }
}
