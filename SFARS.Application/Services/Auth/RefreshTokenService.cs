using MapsterMapper;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Auth;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;

namespace SFARS.Application.Services.Auth
{
    public class RefreshTokenService : GenericService<RefreshToken, RefreshTokenDto, int>, 
        IRefreshTokenService<RefreshTokenDto>
    {
        public RefreshTokenService(
            ISystemMessageService msgService,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<RefreshTokenService> logger) : base(msgService, unitOfWork, mapper, logger)
        {
        }

        /// <summary>
        /// Get refresh token by user ID
        /// </summary>
        public async Task<IServiceResult> GetByUserIdAsync(Guid userId)
        {
            var refreshToken = await _unitOfWork.Repository<RefreshToken, int>().GetWithSpecAsync(
                    new BaseSpecification<RefreshToken>(r => r.UserId == userId));

            if (refreshToken is null)
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Data not found or empty");
            }

            return new ServiceResult(ResultCodeConst.SYS_Success0002, "Get data successfully",
                _mapper.Map<RefreshTokenDto>(refreshToken));
        }

        public async Task<IServiceResult> GetByTokenIdAndRefreshTokenIdAsync(string tokenId, string refreshTokenId)
        {
            try
            {
                var refreshToken = await _unitOfWork.Repository<RefreshToken, int>().GetWithSpecAsync(
                    new BaseSpecification<RefreshToken>(r => r.TokenId == tokenId
                                                             && r.RefreshTokenId == refreshTokenId));

                if (refreshToken is null)
                {
                    return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Data not found or empty or empty");
                }

                return new ServiceResult(ResultCodeConst.SYS_Success0002, "Get data successfully",
                    _mapper.Map<RefreshTokenDto>(refreshToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw new Exception("Error invoke when progress get token and refresh token");
            }
        }

        /// <summary>
        /// Get refresh token by rescuer ID (alias for GetByUserIdAsync for compatibility)
        /// </summary>
        //public async Task<IServiceResult<RefreshTokenDto>> GetByRecuserIdAsync(Guid rescuerId)
        //{
        //    // In the current model, rescuers use the same User entity
        //    // This method exists for interface compatibility
        //    return await GetByUserIdAsync(rescuerId);
        //}
    }
}
