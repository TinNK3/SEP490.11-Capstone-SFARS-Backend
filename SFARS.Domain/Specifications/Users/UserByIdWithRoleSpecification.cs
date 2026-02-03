using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Entities;

namespace SFARS.Domain.Specifications.Users
{
    public class UserByIdWithRoleSpecification : BaseSpecification<User>
    {
        public UserByIdWithRoleSpecification(Guid userId)
            : base(u => u.Id == userId)
        {
            EnableSplitQuery();
            ApplyInclude(q => q
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role));
        }
    }
}