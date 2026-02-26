using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services
{
    public interface IAuthService<TDto>
        where TDto : class
    {
        Task<IServiceResult> SignInAsync(string email);
        Task<IServiceResult> SignInWithPasswordAsync(TDto user);
        Task<IServiceResult> SignInWithGoogleAsync(string googleIdToken);
        Task<IServiceResult> SignInWithOtpAsync(string otp, TDto user);
        Task<IServiceResult> SignUpAsync(TDto user);
        Task<IServiceResult> ForgotPasswordAsync(string email);
        Task<IServiceResult> ResetPasswordAsync(string email, string otp, string newPassword);
        Task<IServiceResult> RefreshTokenAsync(string refreshTokenId, string accessToken);
        Task<IServiceResult> SignOutAsync(Guid userId, string accessToken);

        // OTP Management
        Task<IServiceResult> SendOtpAsync(string email, string purpose);
        Task<IServiceResult> VerifyOtpAsync(string email, string otp, string purpose);
    }
}