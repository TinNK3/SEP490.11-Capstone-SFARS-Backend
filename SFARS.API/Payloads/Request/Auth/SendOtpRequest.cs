namespace SFARS.API.Payloads.Request.Auth
{
    public class SendOtpRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
    }
}
