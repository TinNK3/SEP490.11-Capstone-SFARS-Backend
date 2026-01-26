using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Entities;

namespace SFARS.Domain.Specifications
{
    /// <summary>
    /// Specifications for querying User entities
    /// </summary>
    public class UserSpecification : BaseSpecification<User>
    {
        /// <summary>
        /// Default constructor - get all users ordered by email
        /// </summary>
        public UserSpecification() : base()
        {
            AddOrderBy(u => u.Email);
        }

        /// <summary>
        /// Get user by ID with roles included
        /// </summary>
        public UserSpecification(Guid id) : base(u => u.Id == id)
        {
            ApplyInclude(q => q.Include(u => u.UserRoles).ThenInclude(ur => ur.Role));
        }

        /// <summary>
        /// Get user by email with roles included (for authentication)
        /// </summary>
        public static UserSpecification ByEmail(string email)
        {
            var spec = new UserSpecification();
            spec.AddFilter(u => u.Email.ToLower() == email.ToLower());
            spec.ApplyInclude(q => q.Include(u => u.UserRoles).ThenInclude(ur => ur.Role));
            return spec;
        }

        /// <summary>
        /// Search users by name or email
        /// </summary>
        public static UserSpecification SearchByNameOrEmail(string searchTerm)
        {
            var spec = new UserSpecification();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                spec.AddFilter(u => 
                    u.Email.Contains(searchTerm) || 
                    u.FirstName.Contains(searchTerm) || 
                    u.LastName.Contains(searchTerm));
            }
            return spec;
        }

        /// <summary>
        /// Get users with pagination
        /// </summary>
        public static UserSpecification WithPagination(int pageIndex, int pageSize)
        {
            var spec = new UserSpecification();
            spec.ApplyPaging(pageSize, pageIndex * pageSize);
            spec.AddOrderBy(u => u.Email);
            return spec;
        }
    }
}
