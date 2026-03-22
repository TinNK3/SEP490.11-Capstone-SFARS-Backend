using SFARS.Domain.Common.Enum;

namespace SFARS.API.Payloads.Request.Admin;

/// <summary>
/// Request payload for admin to update a user's information.
/// Admin can update user profile fields (name, contact info, etc.).
/// </summary>
public class UpdateUserRequest
{
    /// <summary>
    /// User's first name
    /// </summary>
    public string FirstName { get; set; } = null!;

    /// <summary>
    /// User's last name
    /// </summary>
    public string LastName { get; set; } = null!;

    /// <summary>
    /// User's phone number (optional)
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// User's avatar URL (optional)
    /// </summary>
    public string? Avatar { get; set; }

    /// <summary>
    /// User's address (optional)
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// User's gender (optional)
    /// </summary>
    public Gender? Gender { get; set; }

    /// <summary>
    /// User's date of birth (optional)
    /// </summary>
    public DateTime? Dob { get; set; }

    /// <summary>
    /// User's status (optional). If provided, admin can update status in the same PUT request.
    /// </summary>
    public UserStatus? Status { get; set; }

    /// <summary>
    /// User's role (optional). If provided, admin can update role in the same PUT request.
    /// </summary>
    public RoleType? Role { get; set; }
}
