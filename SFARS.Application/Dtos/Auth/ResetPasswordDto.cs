namespace SFARS.Application.Dtos.Auth
{
    /// <summary>
    /// DTO for reset password request
    /// </summary>
    public class ResetPasswordDto
    {
        public string Email { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
