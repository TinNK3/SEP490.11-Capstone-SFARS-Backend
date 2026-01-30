using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services
{
    public interface ISystemRoleService<TDto> : IGenericService<Role, TDto, Guid>
        where TDto : class
    {
        Task<IServiceResult> GetRoleByNameAsync(string roleName);
    }
}