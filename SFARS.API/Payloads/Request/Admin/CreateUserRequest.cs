using SFARS.Domain.Common.Enum;

namespace SFARS.API.Payloads.Request.Admin
{
    /// <summary>
    /// Request payload for Admin creating a new user account.
    /// </summary>
    public class CreateUserRequest
    {
        public string   FirstName { get; set; } = null!;
        public string   LastName  { get; set; } = null!;
        public string   Email     { get; set; } = null!;
        public string   Password  { get; set; } = null!;
        public string?  Phone     { get; set; }

        /// <summary>Role to assign: User | Rescuer | Admin</summary>
        public RoleType Role      { get; set; }
    }
}
