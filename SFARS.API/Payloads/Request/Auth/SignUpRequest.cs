namespace SFARS.API.Payloads.Request.Auth
{
    public class SignUpRequest
    {
        public string? UserCode { get; set; }
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
    }
}
