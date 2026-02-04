namespace SFARS.API.Payloads.Request.Auth
{
    public class SignInRequest
    {
        // [Required]
        // [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
