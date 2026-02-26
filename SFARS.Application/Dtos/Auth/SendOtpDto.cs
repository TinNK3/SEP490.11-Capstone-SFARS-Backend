using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Auth
{
    /// <summary>
    /// DTO for send OTP request
    /// </summary>
    public class SendOtpDto
    {
        public string Email { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
    }
}
