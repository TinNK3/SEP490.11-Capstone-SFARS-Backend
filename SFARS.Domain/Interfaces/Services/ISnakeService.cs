using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Interfaces.Services
{
    public record SnakeImageUploadInfo(Stream Stream, string FileName, string ContentType);

    public interface ISnakeService<TDto> : IGenericService<Snake, TDto, Guid>
        where TDto : class
    {
        Task<IServiceResult> GetSnakeById(Guid id);
        Task<IServiceResult> CreateSnake(TDto dto);
        Task<IServiceResult> DeleteSnake(Guid id);
        Task<IServiceResult> GetAllSnakesAsync(SnakeSpecParams specParams);

        /// <summary>
        /// Update snake with field-level change detection and audit logging.
        /// </summary>
        Task<IServiceResult> UpdateSnakeAsync(Guid id, TDto dto, string? changeReason);

        /// <summary>
        /// Preview a CSV/Excel import.
        /// </summary>
        Task<IServiceResult> PreviewImportAsync(Stream excelStream);

        /// <summary>
        /// Apply an Excel import.
        /// </summary>
        Task<IServiceResult> ApplyImportAsync(Stream excelStream, string? changeReason);

        /// <summary>
        /// Get change history for a specific snake with pagination.
        /// </summary>
        Task<IServiceResult> GetSnakeChangeHistory(Guid snakeId, BaseSpecParams specParams);

        /// <summary>
        /// Revert a specific field change using its ChangeLog entry ID.
        /// </summary>
        Task<IServiceResult> RevertSnakeField(Guid changeLogId);

        /// <summary>
        /// Update snake images management.
        /// </summary>
        Task<IServiceResult> UpdateSnakeImagesAsync(
            Guid snakeId, 
            List<Guid>? keepImageIds, 
            Guid? primaryExistingImageId, 
            List<SnakeImageUploadInfo>? newImages, 
            int? primaryNewImageIndex);
    }
}