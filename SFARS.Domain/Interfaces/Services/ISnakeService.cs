using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services
{
    public interface ISnakeService<TDto> : IGenericService<Snake, TDto, int>
        where TDto : class
    {
        Task<IServiceResult> GetSnakeById(int id);
        Task<IServiceResult> CreateSnake(TDto dto);
        Task<IServiceResult> DeleteSnake(int id);
        Task<IServiceResult> SearchSnakes(string? searchTerm, int pageIndex = 0, int pageSize = 10);
        Task<IServiceResult> GetAllSnakesPaginated(int pageIndex = 0, int pageSize = 10);
    }
}