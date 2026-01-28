using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFARS.Domain.Interfaces.Services
{
    public interface ISystemRoleService<TDto> : IGenericService<Role, TDto, Guid>
        where TDto : class
    {
        Task<IServiceResult> GetRoleByNameAsync(string roleName);
    }
}
