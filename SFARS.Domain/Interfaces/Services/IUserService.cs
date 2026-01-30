using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services
{
    public interface IUserService<TDto> : IGenericService<User, TDto, Guid>
        where TDto : class
    {
        Task<IServiceResult> GetByEmailAsync(string email);
    }
}