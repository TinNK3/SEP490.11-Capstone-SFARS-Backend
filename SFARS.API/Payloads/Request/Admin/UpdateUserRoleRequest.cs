using System.ComponentModel.DataAnnotations;
using SFARS.Domain.Common.Enum;

namespace SFARS.API.Payloads.Request.Admin;

/// <summary>
/// Request payload for updating a user's role.
/// </summary>
public class UpdateUserRoleRequest
{
    /// <summary>
    /// The new role name to assign. E.g. "User", "Rescuer", "Admin".
    /// </summary>
    [Required]
    public RoleType RoleName { get; set; }
}
