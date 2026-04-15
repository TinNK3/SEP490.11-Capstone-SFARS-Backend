using System.ComponentModel;

namespace SFARS.API.Payloads.Request.Auth
{
    public class DeleteAccountRequest
    {
        [DefaultValue("123456")]
        public string Otp { get; set; } = string.Empty;
    }
}