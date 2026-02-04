namespace SFARS.API.Payloads.Request.Auth
{
    public class SignInWithOtpRequest
    {
        public string Email { get; set; } = null!;
        public string Otp { get; set; } = null!;
    }
}
