using Microsoft.IdentityModel.Tokens;
using SFARS.Application.Configurations;
using SFARS.Application.Dtos.Auth;
using SFARS.Domain.Common.Constants;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SFARS.Application.Utils
{
    public class JwtUtils
    {
        private readonly WebTokenSettings _webTokenSettings;
        private readonly TokenValidationParameters _tokenValidationParameters;

        public JwtUtils()
        {
            _webTokenSettings = null!;
            _tokenValidationParameters = null!;
        }

        public JwtUtils(
            WebTokenSettings webTokenSettings)
        {
            _webTokenSettings = webTokenSettings;
            _tokenValidationParameters = null!;
        }

        public JwtUtils(
            TokenValidationParameters tokenValidationParameters)
        {
            _webTokenSettings = null!;
            _tokenValidationParameters = tokenValidationParameters;
        }

        // Generate JWT token 
        public async Task<(string AccessToken, DateTime ValidTo)> GenerateJwtTokenAsync(
            string tokenId, AuthenticateUserDto user)
        {
            //Get security key
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_webTokenSettings.IssuerSigningKey));

            // JWT token handler
            var jwtTokenHandler = new JwtSecurityTokenHandler();

            //Token claims
            List<Claim> authClaims = new()
            {
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
                Expires = DateTime.UtcNow.AddMinutes(_webTokenSettings.TokenLifeTimeInMinutes),
                Issuer = _webTokenSettings.ValidIssuer,
                Audience = _webTokenSettings.ValidAudience,
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
    }
}
