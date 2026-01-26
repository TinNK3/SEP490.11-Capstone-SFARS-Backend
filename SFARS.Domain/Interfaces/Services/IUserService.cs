using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFARS.Domain.Interfaces.Services
{
    public interface IUserService<TDto> : IGenericService<User, TDto, Guid>
        where TDto : class
    {
        Task<IServiceResult> GetByEmailAsync(string email);
    }
}
