using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SFARS.Application.Configurations;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Exceptions;
using SFARS.Domain.Common.Constants;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SFARS.Application.Utils
{
    public class JwtUtils : IJwtUtils
    {
        private readonly WebTokenSettings _settings;
        private readonly SymmetricSecurityKey _signingKey;
        private readonly ILogger<JwtUtils> _logger;

        public JwtUtils(IOptions<WebTokenSettings> options, ILogger<JwtUtils> logger)
        {
            _settings = options.Value;
            _signingKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_settings.IssuerSigningKey));
            _logger = logger;
        }

        // Generate JWT token 
        public async Task<(string AccessToken, DateTime ValidTo)> GenerateJwtTokenAsync(
            string tokenId, AuthUserDto user)
        {
            //Get security key
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.IssuerSigningKey));

            // JWT token handler
            var jwtTokenHandler = new JwtSecurityTokenHandler();

            //Token claims
            List<Claim> authClaims = new()
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.RoleName),
                new Claim(CustomClaimTypes.UserType, user.IsRescuer
                    ? ClaimValues.RESCUER_CLAIMVALUE // Is rescuer
                    : ClaimValues.USER_CLAIMVALUE), // Is normal user                             
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, $"{user.FirstName}{user.LastName}".Trim()),
                new Claim(JwtRegisteredClaimNames.Jti, tokenId) // JWT ID
            };

            //Token descriptor
            var tokenDescriptor = new SecurityTokenDescriptor()
            {
                // Token claims (email, role, username, id...)
                Subject = new ClaimsIdentity(authClaims),
                Expires = DateTime.UtcNow.AddMinutes(_settings.TokenLifeTimeInMinutes),
                Issuer = _settings.ValidIssuer,
                Audience = _settings.ValidAudience,
                SigningCredentials = new SigningCredentials(
                    authSigningKey, SecurityAlgorithms.HmacSha256)
            };

            // Generate token with descriptor
            var token = jwtTokenHandler.CreateToken(tokenDescriptor);
            return await Task.FromResult((jwtTokenHandler.WriteToken(token), token.ValidTo));
        }

        // Generate refresh token
        public async Task<string> GenerateRefreshTokenAsync()
        {
            var randomNumber = new byte[64];

            using (var numberGenerator = RandomNumberGenerator.Create())
            {
                numberGenerator.GetBytes(randomNumber);
            }

            return await Task.FromResult(Convert.ToBase64String(randomNumber));
        }

        /// <summary>
        /// Extract expỉred token
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string accessToken)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var signingKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_settings.IssuerSigningKey));


            var validationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,

                ValidIssuer = _settings.ValidIssuer,
                ValidAudience = _settings.ValidAudience,

                ValidateLifetime = false // allow expired token
            };

            try
            {
                var principal = tokenHandler.ValidateToken(accessToken, validationParameters, out var securityToken);

                if (securityToken is not JwtSecurityToken jwtToken ||
                    !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256,
                        StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }

                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to validate access token for refresh. Issuer: {Issuer}, Audience: {Audience}",
                    _settings.ValidIssuer, _settings.ValidAudience);
                return null;
            }
        }
    }
}