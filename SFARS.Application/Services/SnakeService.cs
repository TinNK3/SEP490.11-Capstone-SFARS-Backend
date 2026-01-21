using MapsterMapper;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;

namespace SFARS.Application.Services
{
    public class SnakeService : GenericService<Snake, SnakeDto, int>, ISnakeService<SnakeDto>
    {
        public SnakeService(
            ISystemMessageService msgService, 
            IUnitOfWork unitOfWork, 
            IMapper mapper, 
            ILogger<SnakeService> logger) 
            : base(msgService, unitOfWork, mapper, logger)
        {
        }

        /// <summary>
        /// Get snake by ID
        /// </summary>
        public async Task<IServiceResult> GetSnakeById(int id)
        {
            try
            {
                var spec = new SnakeSpecification(id);
                var result = await GetWithSpecAsync(spec);
                
                if (result.Data == null)
                {
                    return new ServiceResult(
                        ResultCodeConst.SYS_Warning0004,
                        "Snake not found");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting snake by ID: {SnakeId}", id);
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0002,
                    $"Error retrieving snake: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a new snake
        /// </summary>
        public async Task<IServiceResult> CreateSnake(SnakeDto dto)
        {
            try
            {
                // Validate
                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    return new ServiceResult(
                        ResultCodeConst.SYS_Warning0001,
                        "Snake name is required");
                }

                // Check if snake with same name already exists
                var existingSpec = SnakeSpecification.ByNameContains(dto.Name);
                var existing = await GetAllWithSpecAsync(existingSpec);
                
                if (existing.Data != null && ((System.Collections.IEnumerable)existing.Data).Cast<object>().Any())
                {
                    return new ServiceResult(
                        ResultCodeConst.SYS_Warning0003,
                        $"A snake with name '{dto.Name}' already exists");
                }

                // Create
                var result = await CreateAsync(dto);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating snake: {SnakeName}", dto.Name);
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    $"Error creating snake: {ex.Message} {ex.InnerException?.Message}");
            }
        }

        /// <summary>
        /// Delete snake by ID
        /// </summary>
        public async Task<IServiceResult> DeleteSnake(int id)
        {
            try
            {
                // Check if snake exists
                var spec = new SnakeSpecification(id);
                var existing = await GetWithSpecAsync(spec);
                
                if (existing.Data == null)
                {
                    return new ServiceResult(
                        ResultCodeConst.SYS_Warning0004,
                        "Snake not found");
                }

                // Delete
                var result = await DeleteAsync(id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting snake: {SnakeId}", id);
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0004,
                    "Error deleting snake");
            }
        }

        /// <summary>
        /// Search snakes by name with pagination
        /// </summary>
        public async Task<IServiceResult> SearchSnakes(string? searchTerm, int pageIndex = 0, int pageSize = 10)
        {
            try
            {
                var spec = SnakeSpecification.SearchWithPagination(searchTerm, pageIndex, pageSize);
                var result = await GetAllWithSpecAsync(spec);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching snakes");
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0002,
                    "Error searching snakes");
            }
        }

        /// <summary>
        /// Get all snakes with pagination
        /// </summary>
        public async Task<IServiceResult> GetAllSnakesPaginated(int pageIndex = 0, int pageSize = 10)
        {
            try
            {
                var spec = SnakeSpecification.WithPagination(pageIndex, pageSize);
                var result = await GetAllWithSpecAsync(spec);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated snakes");
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0002,
                    "Error retrieving snakes");
            }
        }
    }
}