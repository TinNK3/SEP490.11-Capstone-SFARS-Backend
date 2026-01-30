namespace SFARS.API.Payloads.Request.Auth
{
    public class SignInWithPasswordRequest
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}