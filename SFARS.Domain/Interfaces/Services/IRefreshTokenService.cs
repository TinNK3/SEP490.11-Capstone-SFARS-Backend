using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFARS.Domain.Interfaces.Services
{
    public interface IRefreshTokenService<TDto> : IGenericService<RefreshToken, TDto, int>
        where TDto : class
    {
        Task<IServiceResult<TDto>> GetByUserIdAsync(Guid userId);
        Task<IServiceResult<TDto>> GetByRecuserIdAsync(Guid employeeId);
    }
}
