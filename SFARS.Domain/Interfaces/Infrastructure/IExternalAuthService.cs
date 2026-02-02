using SFARS.Domain.Models;

namespace SFARS.Domain.Interfaces.Infrastructure
{
    public interface IExternalAuthService
    {
        Task<ExternalAuthUser> VerifyGoogleTokenAsync(string idToken);
    }
}