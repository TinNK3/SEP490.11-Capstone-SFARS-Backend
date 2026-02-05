namespace SFARS.Application.Dtos.Auth
{
    /// <summary>
    /// DTO for SignIn method response - indicates which authentication method to use
    /// </summary>
    public class SignInMethodDto
    {
        /// <summary>
        /// Authentication method: "password" or "otp"
        /// </summary>
        public string Method { get; set; } = string.Empty;
    }
}
