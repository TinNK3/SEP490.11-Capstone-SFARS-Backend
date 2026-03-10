using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services
{
    public interface IFaqService<TDto> : IReadOnlyService<Faq, TDto, Guid>
        where TDto : class
    {
        /// <summary>
        /// [Admin] Create a new FAQ
        /// </summary>
        Task<IServiceResult> CreateFaqAsync(TDto dto);

        /// <summary>
        /// [Admin] Update an existing FAQ
        /// </summary>
        Task<IServiceResult> UpdateFaqAsync(Guid id, TDto dto);

        /// <summary>
        /// [Admin] Delete (soft delete) a FAQ by setting IsActive = false
        /// </summary>
        Task<IServiceResult> DeleteFaqAsync(Guid id);

        /// <summary>
        /// [Public] Get a single FAQ by ID
        /// </summary>
        Task<IServiceResult> GetFaqByIdAsync(Guid id);

        /// <summary>
        /// [Public] Get all active FAQs (IsActive = true), sorted by Order
        /// </summary>
        Task<IServiceResult> GetAllActiveFaqsAsync();

        /// <summary>
        /// [Admin] Get all FAQs with pagination (including inactive)
        /// </summary>
        Task<IServiceResult> GetAllFaqsPaginatedAsync(int pageIndex = 0, int pageSize = 10);
    }
}
