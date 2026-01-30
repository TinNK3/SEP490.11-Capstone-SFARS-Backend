using SFARS.Application.Dtos.User;

namespace SFARS.Application.Dtos.Auth
{
    public class AuthResultDto
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public DateTime ValidTo { get; set; }
        public UserDto User { get; set; } = null!;
    }
}