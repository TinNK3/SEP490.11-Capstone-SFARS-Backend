using SFARS.Application.Dtos.Auth;

namespace SFARS.Application.Utils
{
    public interface IJwtUtils
    {
        Task<(string AccessToken, DateTime ValidTo)> GenerateJwtTokenAsync(string tokenId, AuthUserDto user);
        Task<string> GenerateRefreshTokenAsync();
    }
}