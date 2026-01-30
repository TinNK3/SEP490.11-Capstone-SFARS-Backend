using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services
{
    public interface IAuthService<TDto>
        where TDto : class
    {
        Task<IServiceResult> SignInWithPasswordAsync(TDto user);
        Task<IServiceResult> SignUpAsync(TDto user);
    }
}