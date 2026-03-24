namespace SFARS.Application.Dtos.Auth
{
    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
        public string Otp { get; set; } = null!;
    }
}
