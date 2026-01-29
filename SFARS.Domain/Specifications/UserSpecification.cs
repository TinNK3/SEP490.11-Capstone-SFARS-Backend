using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Entities;
using SFARS.Domain.Specifications.Params;
using System.Linq.Expressions;

namespace SFARS.Domain.Specifications
{
    /// <summary>
    /// Specifications for querying User entities
    /// </summary>
    public class UserSpecification : BaseSpecification<User>
    {

        public int PageIndex { get; set; }
        public int PageSize { get; set; }

        public UserSpecification(UserSpecParams userSpecParams, int pageIndex, int pageSize)
            : base(e =>
                // Search with terms
                string.IsNullOrEmpty(userSpecParams.Search) ||
                (
                    // Email
                    (!string.IsNullOrEmpty(e.Email) && e.Email.Contains(userSpecParams.Search)) ||
                    // Phone
                    (!string.IsNullOrEmpty(e.Phone) && e.Phone.Contains(userSpecParams.Search)) ||
                    // Address
                    (!string.IsNullOrEmpty(e.Address) && e.Address.Contains(userSpecParams.Search)) ||
                    // Individual FirstName and LastName search
                    (!string.IsNullOrEmpty(e.FirstName) && e.FirstName.Contains(userSpecParams.Search)) ||
                    (!string.IsNullOrEmpty(e.LastName) && e.LastName.Contains(userSpecParams.Search)) ||
                    // Full Name search
                    (!string.IsNullOrEmpty(e.FirstName) &&
                     !string.IsNullOrEmpty(e.LastName) &&
                     (e.FirstName + " " + e.LastName).Contains(userSpecParams.Search))
                ))
        {
            PageIndex = pageIndex;
            PageSize = pageSize;
            // Enable split query
            EnableSplitQuery();
            // Include role 
            ApplyInclude(q => q
                .Include(e => e.UserRoles)
                .ThenInclude(ur => ur.Role));
            // Default order by first name
            AddOrderBy(e => e.FirstName);
            if (!string.IsNullOrEmpty(userSpecParams.FirstName)) // With first name
            {
                AddFilter(x => x.FirstName == userSpecParams.FirstName);
            }

            if (!string.IsNullOrEmpty(userSpecParams.LastName)) // With last name
            {
                AddFilter(x => x.LastName == userSpecParams.LastName);
            }

            if (userSpecParams.Gender != null) // With gender
            {
                AddFilter(x => x.Gender == userSpecParams.Gender);
            }

            if (userSpecParams.Status != null) // With status
            {
                AddFilter(x => x.Status == userSpecParams.Status);
            }

            if (userSpecParams.DobRange != null
                && userSpecParams.DobRange.Length > 1) // With range of dob
            {
                if (userSpecParams.DobRange[0] is null && userSpecParams.DobRange[1].HasValue)
                {
                    AddFilter(x => x.Dob <= userSpecParams.DobRange[1]);
                }
                else if (userSpecParams.DobRange[0].HasValue && userSpecParams.DobRange[1] is null)
                {
                    AddFilter(x => x.Dob >= userSpecParams.DobRange[0]);
                }
                else
                {
                    AddFilter(x => x.Dob.HasValue &&
                                   x.Dob.Value.Date >= userSpecParams.DobRange[0].Value.Date
                                   && x.Dob.Value.Date <= userSpecParams.DobRange[1].Value.Date);
                }
            }

            // Apply Sorting
            if (!string.IsNullOrEmpty(userSpecParams.Sort))
            {
                var sortBy = userSpecParams.Sort.Trim();
                var isDescending = sortBy.StartsWith("-");
                var propertyName = isDescending ? sortBy.Substring(1) : sortBy;

                ApplySorting(propertyName, isDescending);
            }
            else
            {
                // Default order by create date
                AddOrderByDescending(u => u.CreatedAt);
            }

            // Exclude Admin users
            AddFilter(x => !x.UserRoles.Any(ur => ur.Role.RoleName == "Admin"));
        }

        private void ApplySorting(string propertyName, bool isDescending)
        {
            if (string.IsNullOrEmpty(propertyName)) return;

            // Use Reflection to dynamically apply sorting
            var parameter = Expression.Parameter(typeof(User), "x");
            var property = Expression.Property(parameter, propertyName);
            var sortExpression =
                Expression.Lambda<Func<User, object>>(Expression.Convert(property, typeof(object)), parameter);

            if (isDescending)
            {
                AddOrderByDescending(sortExpression);
            }
            else
            {
                AddOrderBy(sortExpression);
            }
        }
    }
}
