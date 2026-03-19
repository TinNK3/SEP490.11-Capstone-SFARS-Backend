using SFARS.Domain.Common.Enum;

namespace SFARS.API.Payloads.Request.User
{
    /// <summary>
    /// Request payload for updating user profile
    /// </summary>
    public class UpdateProfileRequest
    {
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Avatar { get; set; }
        public string? Address { get; set; }
        public Gender? Gender { get; set; }
        public DateTime? Dob { get; set; }
    }
}