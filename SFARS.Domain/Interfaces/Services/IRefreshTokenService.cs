using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services
{
    public interface IRefreshTokenService<TDto> : IGenericService<RefreshToken, TDto, int>
        where TDto : class
    {
        Task<IServiceResult> GetByUserIdAsync(Guid userId);
        Task<IServiceResult> GetByTokenIdAndRefreshTokenIdAsync(string tokenId, string refreshTokenId);
    }
}