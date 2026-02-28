using System.ComponentModel;
using SFARS.Domain.Common.Enum;

namespace SFARS.API.Payloads.Request.Auth
{
    public class VerifyOtpRequest
    {
        [DefaultValue("example@gmail.com")]
        public string Email { get; set; } = string.Empty;

        [DefaultValue("123456")]
        public string Otp { get; set; } = string.Empty;

        [DefaultValue(OtpType.SignIn)]
        public OtpType Type { get; set; }
    }
}
