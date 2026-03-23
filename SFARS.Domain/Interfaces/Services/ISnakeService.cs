using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Interfaces.Services
{
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
        /// Preview a CSV import (parse + validate, no DB write).
        /// </summary>
        Task<IServiceResult> PreviewImportAsync(Stream csvStream);

        /// <summary>
        /// Apply a CSV import (upsert by ScientificName with audit logging).
        /// </summary>
        Task<IServiceResult> ApplyImportAsync(Stream csvStream, string? changeReason);

        /// <summary>
        /// Get change history for a specific snake with pagination.
        /// </summary>
        Task<IServiceResult> GetSnakeChangeHistory(Guid snakeId, BaseSpecParams specParams);

        /// <summary>
        /// Revert a specific field change using its ChangeLog entry ID.
        /// </summary>
        Task<IServiceResult> RevertSnakeField(Guid changeLogId);
    }
}