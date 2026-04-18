using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services;

public record FirstAidImageUploadInfo(Stream Stream, string FileName, string ContentType);

/// <summary>
/// Admin management of First Aid Detail steps.
/// </summary>
public interface IFirstAidDetailService
{
    /// <summary>[Public] Get all first aid procedures grouped by ToxinGroup.</summary>
    Task<IServiceResult> GetAllGroupedAsync(string? toxinGroup);

    /// <summary>[Admin] Get a single first aid detail by ID.</summary>
    Task<IServiceResult> GetByIdAsync(Guid id);

    /// <summary>[Admin] Create a new first aid detail step.</summary>
    Task<IServiceResult> CreateAsync<TDto>(Guid adminId, TDto dto, FirstAidImageUploadInfo? imageInfo = null) where TDto : class;

    /// <summary>[Admin] Update an existing first aid detail step.</summary>
    Task<IServiceResult> UpdateAsync<TDto>(Guid id, Guid adminId, TDto dto, FirstAidImageUploadInfo? imageInfo = null) where TDto : class;

    /// <summary>[Admin] Delete a first aid detail step.</summary>
    Task<IServiceResult> DeleteAsync(Guid id, Guid adminId);
}