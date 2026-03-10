using MapsterMapper;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Faq;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Application.Services.Faq
{
    public class FaqService : ReadOnlyService<Domain.Entities.Faq, FaqDto, Guid>, IFaqService<FaqDto>
    {
        public FaqService(
            ISystemMessageService msgService,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<FaqService> logger)
            : base(msgService, unitOfWork, mapper, logger)
        {
        }

        #region Admin CRUD Operations

        /// <summary>
        /// [Admin] Create a new FAQ
        /// </summary>
        public async Task<IServiceResult> CreateFaqAsync(FaqDto dto)
        {
            try
            {
                // Validation
                if (string.IsNullOrWhiteSpace(dto.Question))
                {
                    return new ServiceResult(
                        ResultCodeConst.SYS_Warning0001,
                        await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));
                }

                if (string.IsNullOrWhiteSpace(dto.Answer))
                {
                    return new ServiceResult(
                        ResultCodeConst.SYS_Warning0001,
                        await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));
                }

                // Check for duplicate question
                var isDuplicate = await _unitOfWork.Repository<Domain.Entities.Faq, Guid>()
                    .AnyAsync(f => f.Question.Trim().ToLower() == dto.Question.Trim().ToLower());

                if (isDuplicate)
                {
                    return new ServiceResult(
                        ResultCodeConst.SYS_Warning0003,
                        "A FAQ with the same question already exists");
                }

                // Auto-assign order if not provided
                if (dto.Order == 0)
                {
                    var maxOrder = await _unitOfWork.Repository<Domain.Entities.Faq, Guid>()
                        .GetAllAsync();
                    dto.Order = maxOrder.Any() ? maxOrder.Max(f => f.Order) + 1 : 1;
                }

                // Map and create entity
                var entity = _mapper.Map<Domain.Entities.Faq>(dto);
                entity.Id = Guid.NewGuid();
                entity.CreatedAt = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.IsActive = dto.IsActive;

                await _unitOfWork.Repository<Domain.Entities.Faq, Guid>().AddAsync(entity);
                await _unitOfWork.SaveChangesAsync();

                var resultDto = _mapper.Map<FaqDto>(entity);

                return new ServiceResult(
                    ResultCodeConst.SYS_Success0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001),
                    resultDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating FAQ");
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
            }
        }

        /// <summary>
        /// [Admin] Update an existing FAQ
        /// </summary>
        public async Task<IServiceResult> UpdateFaqAsync(Guid id, FaqDto dto)
        {
            try
            {
                var entity = await _unitOfWork.Repository<Domain.Entities.Faq, Guid>().GetByIdAsync(id);

                if (entity == null)
                {
                    return new ServiceResult(
                        ResultCodeConst.SYS_Warning0004,
                        "FAQ not found");
                }

                // Validation
                if (string.IsNullOrWhiteSpace(dto.Question))
                {
                    return new ServiceResult(
                        ResultCodeConst.SYS_Warning0001,
                        await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));
                }

                if (string.IsNullOrWhiteSpace(dto.Answer))
                {
                    return new ServiceResult(
                        ResultCodeConst.SYS_Warning0001,
                        await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));
                }

                // Check for duplicate question (excluding current FAQ)
                var isDuplicate = await _unitOfWork.Repository<Domain.Entities.Faq, Guid>()
                    .AnyAsync(f => f.Id != id && f.Question.Trim().ToLower() == dto.Question.Trim().ToLower());

                if (isDuplicate)
                {
                    return new ServiceResult(
                        ResultCodeConst.SYS_Warning0003,
                        "A FAQ with the same question already exists");
                }

                // Update entity
                entity.Question = dto.Question.Trim();
                entity.Answer = dto.Answer.Trim();
                entity.Order = dto.Order;
                entity.IsActive = dto.IsActive;
                entity.UpdatedAt = DateTime.UtcNow;

                _unitOfWork.Repository<Domain.Entities.Faq, Guid>().Update(entity);
                await _unitOfWork.SaveChangesAsync();

                var resultDto = _mapper.Map<FaqDto>(entity);

                return new ServiceResult(
                    ResultCodeConst.SYS_Success0002,
                    "FAQ updated successfully",
                    resultDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating FAQ with ID: {Id}", id);
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
            }
        }

        /// <summary>
        /// [Admin] Delete (soft delete) a FAQ by setting IsActive = false
        /// </summary>
        public async Task<IServiceResult> DeleteFaqAsync(Guid id)
        {
            try
            {
                var entity = await _unitOfWork.Repository<Domain.Entities.Faq, Guid>().GetByIdAsync(id);

                if (entity == null)
                {
                    return new ServiceResult(
                        ResultCodeConst.SYS_Warning0004,
                        "FAQ not found");
                }

                // Soft delete
                entity.IsActive = false;
                entity.UpdatedAt = DateTime.UtcNow;

                _unitOfWork.Repository<Domain.Entities.Faq, Guid>().Update(entity);
                await _unitOfWork.SaveChangesAsync();

                return new ServiceResult(
                    ResultCodeConst.SYS_Success0002,
                    "FAQ deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting FAQ with ID: {Id}", id);
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
            }
        }

        #endregion

        #region Public Read Operations

        /// <summary>
        /// [Public] Get a single FAQ by ID
        /// </summary>
        public async Task<IServiceResult> GetFaqByIdAsync(Guid id)
        {
            try
            {
                var entity = await _unitOfWork.Repository<Domain.Entities.Faq, Guid>().GetByIdAsync(id);

                if (entity == null)
                {
                    return new ServiceResult(
                        ResultCodeConst.SYS_Warning0004,
                        "FAQ not found");
                }

                var dto = _mapper.Map<FaqDto>(entity);

                return new ServiceResult(
                    ResultCodeConst.SYS_Success0002,
                    string.Empty,
                    dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving FAQ with ID: {Id}", id);
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
            }
        }

        /// <summary>
        /// [Public] Get all active FAQs (IsActive = true), sorted by Order
        /// </summary>
        public async Task<IServiceResult> GetAllActiveFaqsAsync()
        {
            try
            {
                var entities = await _unitOfWork.Repository<Domain.Entities.Faq, Guid>().GetAllAsync();

                var activeFaqs = entities
                    .Where(f => f.IsActive)
                    .OrderBy(f => f.Order)
                    .ToList();

                var dtos = _mapper.Map<List<FaqDto>>(activeFaqs);

                return new ServiceResult(
                    ResultCodeConst.SYS_Success0002,
                    string.Empty,
                    dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active FAQs");
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
            }
        }

        /// <summary>
        /// [Admin] Get all FAQs with pagination (including inactive)
        /// </summary>
        public async Task<IServiceResult> GetAllFaqsPaginatedAsync(int pageIndex = 0, int pageSize = 10)
        {
            try
            {
                var allEntities = await _unitOfWork.Repository<Domain.Entities.Faq, Guid>().GetAllAsync();

                var totalCount = allEntities.Count();
                var faqs = allEntities
                    .OrderBy(f => f.Order)
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize)
                    .ToList();

                var dtos = _mapper.Map<List<FaqDto>>(faqs);

                var result = new
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageIndex = pageIndex,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                return new ServiceResult(
                    ResultCodeConst.SYS_Success0002,
                    string.Empty,
                    result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paginated FAQs");
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
            }
        }

        #endregion
    }
}
