using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Auth
{
    /// <summary>
    /// DTO for verify OTP request
    /// </summary>
    public class VerifyOtpDto
    {
        public string Email { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        public OtpType Type { get; set; }
    }
}
