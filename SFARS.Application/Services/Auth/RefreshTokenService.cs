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
    public class RefreshTokenService : GenericService<RefreshToken, RefreshTokenDto, int>, IRefreshTokenService<RefreshTokenDto>
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
        public async Task<IServiceResult<RefreshTokenDto>> GetByUserIdAsync(Guid userId)
        {
            var spec = RefreshTokenSpecification.ByUserId(userId);
            var token = await _unitOfWork.Repository<RefreshToken, int>().GetWithSpecAsync(spec);

            if (token == null)
            {
                return new ServiceResult<RefreshTokenDto>(ResultCodeConst.SYS_Warning0004, "Refresh token not found");
            }

            return new ServiceResult<RefreshTokenDto>(
                ResultCodeConst.SYS_Success0002, 
                string.Empty, 
                _mapper.Map<RefreshTokenDto>(token));
        }

        /// <summary>
        /// Get refresh token by rescuer ID (alias for GetByUserIdAsync for compatibility)
        /// </summary>
        public async Task<IServiceResult<RefreshTokenDto>> GetByRecuserIdAsync(Guid rescuerId)
        {
            // In the current model, rescuers use the same User entity
            // This method exists for interface compatibility
            return await GetByUserIdAsync(rescuerId);
        }
    }
}
