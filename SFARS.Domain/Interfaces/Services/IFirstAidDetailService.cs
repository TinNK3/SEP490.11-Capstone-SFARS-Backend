using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Interfaces.Services;

/// <summary>
/// Admin management of First Aid Detail steps.
/// </summary>
public interface IFirstAidDetailService
{
    /// <summary>[Admin] Get all first aid details with pagination.</summary>
    Task<IServiceResult> GetAllAsync(BaseSpecParams specParams);

    /// <summary>[Admin] Get a single first aid detail by ID.</summary>
    Task<IServiceResult> GetByIdAsync(Guid id);

    /// <summary>[Admin] Create a new first aid detail step.</summary>
    Task<IServiceResult> CreateAsync<TDto>(Guid adminId, TDto dto) where TDto : class;

    /// <summary>[Admin] Update an existing first aid detail step.</summary>
    Task<IServiceResult> UpdateAsync<TDto>(Guid id, Guid adminId, TDto dto) where TDto : class;

    /// <summary>[Admin] Delete a first aid detail step.</summary>
    Task<IServiceResult> DeleteAsync(Guid id, Guid adminId);
}