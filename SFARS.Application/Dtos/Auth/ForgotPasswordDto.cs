namespace SFARS.Application.Dtos.Auth
{
    /// <summary>
    /// DTO for forgot password request
    /// </summary>
    public class ForgotPasswordDto
    {
        public string Email { get; set; } = string.Empty;
    }
}
