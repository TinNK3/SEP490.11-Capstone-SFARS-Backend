using SFARS.Domain.Common.Enum;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services
{
    public interface IAuthService<TDto>
        where TDto : class
    {
        Task<IServiceResult> SignInAsync(string email);
        Task<IServiceResult> SignInWithPasswordAsync(TDto user);
        Task<IServiceResult> SignInWithGoogleAsync(string googleIdToken, bool isAdminLogin = false);
        Task<IServiceResult> SignInWithOtpAsync(string otp, TDto user);
        Task<IServiceResult> SignUpAsync(TDto user);
        Task<IServiceResult> ForgotPasswordAsync(string email);
        Task<IServiceResult> ResetPasswordAsync(string email, string otp, string newPassword);
        Task<IServiceResult> RefreshTokenAsync(string refreshTokenId, string accessToken);
        Task<IServiceResult> SignOutAsync(Guid userId, string accessToken);
        Task<IServiceResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, string otp, string accessToken);

        // OTP Management
        Task<IServiceResult> SendOtpAsync(string email, OtpType type);
        Task<IServiceResult> VerifyOtpAsync(string email, string otp, OtpType type);
    }
}
