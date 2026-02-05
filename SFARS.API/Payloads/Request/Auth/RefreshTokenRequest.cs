namespace SFARS.API.Payloads.Request.Auth
{
    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = null!;
        public string AccessToken { get; set; } = null!;
    }
}
