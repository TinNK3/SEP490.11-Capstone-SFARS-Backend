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
    public class SnakeService : GenericService<Snake, SnakeDto, Guid>, ISnakeService<SnakeDto>
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
        public async Task<IServiceResult> GetSnakeById(Guid id)
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

        /// <summary>
        /// Create a new snake
        /// </summary>
        public override async Task<IServiceResult> CreateAsync(SnakeDto dto)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(dto.CommonName))
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0001,
                    "Snake common name is required");
            }

            // Check if snake with same name already exists
            var existingSpec = SnakeSpecification.ByNameContains(dto.CommonName);
            var existing = await GetAllWithSpecAsync(existingSpec);
            
            if (existing.Data != null && ((System.Collections.IEnumerable)existing.Data).Cast<SnakeDto>().Any(x => x.CommonName == dto.CommonName))
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0003,
                    $"A snake with name '{dto.CommonName}' already exists");
            }

            // Create
            return await base.CreateAsync(dto);
        }

        /// <summary>
        /// Delete snake by ID
        /// </summary>
        public async Task<IServiceResult> DeleteSnake(Guid id)
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

        /// <summary>
        /// Search snakes by name with pagination
        /// </summary>
        public async Task<IServiceResult> SearchSnakes(string? searchTerm, int pageIndex = 0, int pageSize = 10)
        {
            var spec = SnakeSpecification.SearchWithPagination(searchTerm, pageIndex, pageSize);
            var result = await GetAllWithSpecAsync(spec);
            return result;
        }

        /// <summary>
        /// Get all snakes with pagination
        /// </summary>
        public async Task<IServiceResult> GetAllSnakesPaginated(int pageIndex = 0, int pageSize = 10)
        {
            var spec = SnakeSpecification.WithPagination(pageIndex, pageSize);
            var result = await GetAllWithSpecAsync(spec);
            return result;
        }
        
        // Compatibility method if interface requires original CreateSnake (which was just CreateAsync with checks)
        public async Task<IServiceResult> CreateSnake(SnakeDto dto)
        {
            return await CreateAsync(dto);
        }
    }
}