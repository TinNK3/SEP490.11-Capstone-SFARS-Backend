using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Entities;

namespace SFARS.Domain.Specifications
{
    /// <summary>
    /// Specification for querying Role entities
    /// </summary>
    public class RoleSpecification : BaseSpecification<Role>
    {
        /// <summary>
        /// Get role by ID
        /// </summary>
        /// <param name="id">Role ID</param>
        public RoleSpecification(Guid id) : base(r => r.Id == id)
        {
        }

        /// <summary>
        /// Get role by role name
        /// </summary>
        /// <param name="roleName">Role name (e.g., "User", "Admin", "Rescuer")</param>
        public RoleSpecification(string roleName) : base(r => r.RoleName == roleName)
        {
        }

        #region Static Factory Methods

        /// <summary>
        /// Get role by name
        /// </summary>
        public static RoleSpecification ByName(string roleName) => new(roleName);

        /// <summary>
        /// Get role by ID
        /// </summary>
        public static RoleSpecification ById(Guid id) => new(id);

        /// <summary>
        /// Get all roles with their users
        /// </summary>
        public static RoleSpecification WithUsers()
        {
            var spec = new RoleSpecification();
            spec.ApplyInclude(q => q.Include(r => r.UserRoles).ThenInclude(ur => ur.User));
            return spec;
        }

        #endregion

        /// <summary>
        /// Private constructor for factory methods
        /// </summary>
        private RoleSpecification() : base()
        {
        }
    }
}
