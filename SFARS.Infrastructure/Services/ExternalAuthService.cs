using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Models;
using SFARS.Infrastructure.Configurations;

namespace SFARS.Infrastructure.Services
{
    public class ExternalAuthService : IExternalAuthService
    {
        private readonly GoogleAuthSettings _googleAuthSettings;
        private readonly ILogger<ExternalAuthService> _logger;

        public ExternalAuthService(
            IOptionsMonitor<GoogleAuthSettings> optionsMonitor,
            ILogger<ExternalAuthService> logger)
        {
            _googleAuthSettings = optionsMonitor.CurrentValue;
            _logger = logger;
        }

        public async Task<ExternalAuthUser> VerifyGoogleTokenAsync(string idToken)
        {
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new List<string> { _googleAuthSettings.ClientId }
                };

                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
                
                return new ExternalAuthUser
                {
                    Email = payload.Email,
                    FirstName = payload.GivenName,
                    LastName = payload.FamilyName,
                    Avatar = payload.Picture,
                    ProviderId = payload.Subject // Google ID
                };
            }
            catch (InvalidJwtException ex)
            {
                _logger.LogWarning("Invalid Google Token: {Message}", ex.Message);
                throw new UnauthorizedAccessException("Invalid Google Token."); // Use standard or Domain exception
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating Google Token");
                throw new Exception("Google Authentication failed.");
            }
        }
    }
}