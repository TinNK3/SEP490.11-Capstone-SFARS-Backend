using System.ComponentModel.DataAnnotations;

namespace SFARS.API.Payloads.Request.Auth
{
    public class SignInWithGoogleRequest
    {
        [Required]
        public string Credential { get; set; } = null!;
    }
}
