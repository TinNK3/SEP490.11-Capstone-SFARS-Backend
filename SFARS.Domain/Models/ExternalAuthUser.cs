namespace SFARS.Domain.Models
{
    public class ExternalAuthUser
    {
        public string Email { get; set; } = null!;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Avatar { get; set; }
        public string? ProviderId { get; set; }
    }
}