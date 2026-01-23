using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class Role : BaseEntity
{
    public string RoleName { get; set; } = null!;
    public string? Description { get; set; }

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}