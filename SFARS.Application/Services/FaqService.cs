    using MapsterMapper;
    using Microsoft.Extensions.Logging;
    using SFARS.Application.Common;
    using SFARS.Application.Dtos;
    using SFARS.Application.Dtos.Faq;
    using SFARS.Application.Validations;
    using SFARS.Domain.Entities;
    using SFARS.Domain.Interfaces;
    using SFARS.Domain.Interfaces.Repositories.Base;
    using SFARS.Domain.Interfaces.Services;
    using SFARS.Domain.Interfaces.Services.Base;
    using SFARS.Domain.Specifications.Faqs;

    namespace SFARS.Application.Services
    {
        public class FaqService : GenericService<Faq, FaqDto, Guid>, IFaqService<FaqDto>
        {
            private readonly IUnitOfWork _faqUnitOfWork;
            private readonly ISystemMessageService _faqMsgService;
            private readonly IMapper _faqMapper;
            private readonly ILogger<FaqService> _faqLogger;
            private readonly IGenericRepository<Faq, Guid> _faqRepo;

            public FaqService(
                ISystemMessageService msgService,
                IUnitOfWork unitOfWork,
                IMapper mapper,
                ILogger<FaqService> logger)
                : base(msgService, unitOfWork, mapper, logger)
            {
                _faqUnitOfWork = unitOfWork;
                _faqMsgService = msgService;
                _faqMapper = mapper;
                _faqLogger = logger;
                _faqRepo = _faqUnitOfWork.Repository<Faq, Guid>();
            }

            #region Admin CRUD Operations

            /// <summary>
            /// [Admin] Create a new FAQ
            /// </summary>
            public async Task<IServiceResult> CreateFaqAsync(FaqDto dto)
            {
                try
                {
                    await _faqUnitOfWork.BeginTransactionAsync();

                    var validationResult = await ValidatorExtensions.ValidateAsync(dto);
                    if (validationResult != null)
                    {
                        await _faqUnitOfWork.RollbackTransactionAsync();
                        return new ServiceResult(
                            ResultCodeConst.SYS_Warning0002,
                            string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage)));
                    }

                    var normalizedQuestion = dto.Question.Trim();
                    var isDuplicate = await _faqRepo.AnyAsync(f => f.Question == normalizedQuestion);
                    if (isDuplicate)
                    {
                        await _faqUnitOfWork.RollbackTransactionAsync();
                        return new ServiceResult(
                            ResultCodeConst.SYS_Warning0003,
                            "A FAQ with the same question already exists");
                    }

                    if (dto.Order == 0)
                    {
                        var highestOrderSpec = FaqSpecification.HighestOrder(activeOnly: false);
                        var maxOrderFaq = (await _faqRepo.GetAllWithSpecAsync(highestOrderSpec, false)).FirstOrDefault();
                        dto.Order = (maxOrderFaq?.Order ?? 0) + 1;
                    }

                    var isOrderDuplicate = await _faqRepo.AnyAsync(f => f.Order == dto.Order);
                    if (isOrderDuplicate)
                    {
                        await _faqUnitOfWork.RollbackTransactionAsync();
                        return new ServiceResult(
                            ResultCodeConst.SYS_Warning0003,
                            "A FAQ with the same order already exists");
                    }

                    dto.Question = normalizedQuestion;
                    dto.Answer = dto.Answer.Trim();

                    var entity = _faqMapper.Map<Faq>(dto);
                    entity.Id = Guid.NewGuid();
                    entity.CreatedAt = DateTime.UtcNow;
                    entity.UpdatedAt = DateTime.UtcNow;
                    entity.IsActive = dto.IsActive;

                    await _faqRepo.AddAsync(entity);
                    await _faqUnitOfWork.SaveChangesAsync();
                    await _faqUnitOfWork.CommitTransactionAsync();

                    return new ServiceResult(
                        ResultCodeConst.SYS_Success0001,
                        await _faqMsgService.GetMessageAsync(ResultCodeConst.SYS_Success0001),
                        _faqMapper.Map<FaqDto>(entity));
                }
                catch (Exception ex)
                {
                    await _faqUnitOfWork.RollbackTransactionAsync();
                    _faqLogger.LogError(ex, "Error creating FAQ");
                    return new ServiceResult(
                        ResultCodeConst.SYS_Fail0001,
                        await _faqMsgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
                }
            }

            /// <summary>
            /// [Admin] Update an existing FAQ
            /// </summary>
            public async Task<IServiceResult> UpdateFaqAsync(Guid id, FaqDto dto)
            {
                try
                {
                    await _faqUnitOfWork.BeginTransactionAsync();
                    var validationResult = await ValidatorExtensions.ValidateAsync(dto);
                    if (validationResult != null)
                    {
                        await _faqUnitOfWork.RollbackTransactionAsync();
                        return new ServiceResult(
                            ResultCodeConst.SYS_Warning0002,
                            string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage)));
                    }

                    var entity = await _faqRepo.GetByIdAsync(id);
                    if (entity == null)
                    {
                        await _faqUnitOfWork.RollbackTransactionAsync();
                        return new ServiceResult(
                            ResultCodeConst.SYS_Warning0004,
                            "FAQ not found");
                    }

                    var normalizedQuestion = dto.Question.Trim();
                    var isDuplicate = await _faqRepo.AnyAsync(f => f.Id != id && f.Question == normalizedQuestion);
                    if (isDuplicate)
                    {
                        await _faqUnitOfWork.RollbackTransactionAsync();
                        return new ServiceResult(
                            ResultCodeConst.SYS_Warning0003,
                            "A FAQ with the same question already exists");
                    }

                    var isOrderDuplicate = await _faqRepo.AnyAsync(f => f.Id != id && f.Order == dto.Order);
                    if (isOrderDuplicate)
                    {
                        await _faqUnitOfWork.RollbackTransactionAsync();
                        return new ServiceResult(
                            ResultCodeConst.SYS_Warning0003,
                            "A FAQ with the same order already exists");
                    }

                    entity.Question = normalizedQuestion;
                    entity.Answer = dto.Answer.Trim();
                    entity.Order = dto.Order;
                    entity.IsActive = dto.IsActive;
                    entity.UpdatedAt = DateTime.UtcNow;

                    _faqRepo.Update(entity);
                    await _faqUnitOfWork.SaveChangesAsync();
                    await _faqUnitOfWork.CommitTransactionAsync();

                    return new ServiceResult(
                        ResultCodeConst.SYS_Success0002,
                        "FAQ updated successfully",
                        _faqMapper.Map<FaqDto>(entity));
                }
                catch (Exception ex)
                {
                    await _faqUnitOfWork.RollbackTransactionAsync();
                    _faqLogger.LogError(ex, "Error updating FAQ with ID: {Id}", id);
                    return new ServiceResult(
                        ResultCodeConst.SYS_Fail0001,
                        await _faqMsgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
                }
            }

            /// <summary>
            /// [Admin] Delete FAQ permanently (hard delete)
            /// </summary>
            public async Task<IServiceResult> DeleteFaqAsync(Guid id)
            {
                try
                {
                    await _faqUnitOfWork.BeginTransactionAsync();
                    var rowsAffected = await _faqRepo.DeleteAsync(id);
                    if (rowsAffected == 0)
                    {
                        await _faqUnitOfWork.RollbackTransactionAsync();
                        return new ServiceResult(
                            ResultCodeConst.SYS_Warning0004,
                            "FAQ not found");
                    }

                    await _faqUnitOfWork.CommitTransactionAsync();

                    return new ServiceResult(
                        ResultCodeConst.SYS_Success0004,
                        await _faqMsgService.GetMessageAsync(ResultCodeConst.SYS_Success0004),
                        true);
                }
                catch (Exception ex)
                {
                    await _faqUnitOfWork.RollbackTransactionAsync();
                    _faqLogger.LogError(ex, "Error deleting FAQ with ID: {Id}", id);
                    return new ServiceResult(
                        ResultCodeConst.SYS_Fail0001,
                        await _faqMsgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
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
                    var baseResult = await base.GetByIdAsync(id);
                    if (baseResult.ResultCode == ResultCodeConst.SYS_Warning0004)
                    {
                        return new ServiceResult(
                            ResultCodeConst.SYS_Warning0004,
                            "FAQ not found");
                    }

                    return baseResult;
                }
                catch (Exception ex)
                {
                    _faqLogger.LogError(ex, "Error retrieving FAQ with ID: {Id}", id);
                    return new ServiceResult(
                        ResultCodeConst.SYS_Fail0001,
                        await _faqMsgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
                }
            }

            /// <summary>
            /// [Public] Get all active FAQs (IsActive = true), sorted by Order
            /// </summary>
            public async Task<IServiceResult> GetAllActiveFaqsAsync()
            {
                try
                {
                    var activeFaqs = (await _faqRepo.GetAllWithSpecAsync(FaqSpecification.ActiveOrdered(), false)).ToList();
                    var dtos = _faqMapper.Map<List<FaqDto>>(activeFaqs);

                    return new ServiceResult(
                        ResultCodeConst.SYS_Success0002,
                        string.Empty,
                        dtos);
                }
                catch (Exception ex)
                {
                    _faqLogger.LogError(ex, "Error retrieving active FAQs");
                    return new ServiceResult(
                        ResultCodeConst.SYS_Fail0001,
                        await _faqMsgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
                }
            }

            /// <summary>
            /// [Admin] Get all FAQs with pagination (including inactive)
            /// </summary>
            public async Task<IServiceResult> GetAllFaqsPaginatedAsync(SFARS.Domain.Specifications.Params.BaseSpecParams specParams)
            {
                try
                {
                    var listSpec = FaqSpecification.List(specParams);
                    var countSpec = FaqSpecification.Count(specParams);

                    var faqs = (await _faqRepo.GetAllWithSpecAsync(listSpec, false)).ToList();
                    var totalCount = await _faqRepo.CountAsync(countSpec);

                    var limit = specParams.GetTake();
                    var page = specParams.GetPage();
                    var dtos = _faqMapper.Map<List<FaqDto>>(faqs);
                    var totalPages = (int)Math.Ceiling(totalCount / (double)limit);

                    return new ServiceResult(
                        ResultCodeConst.SYS_Success0002,
                        await _faqMsgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                        new PaginatedResultDto<FaqDto>(dtos, page, limit, totalPages, totalCount));
                }
                catch (Exception ex)
                {
                    _faqLogger.LogError(ex, "Error retrieving paginated FAQs");
                    return new ServiceResult(
                        ResultCodeConst.SYS_Fail0001,
                        await _faqMsgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
                }
            }

            public async Task<IServiceResult> GetSuggestedOrderAsync()
            {
                try
                {
                    var highestActiveOrderFaq = (await _faqRepo.GetAllWithSpecAsync(FaqSpecification.HighestOrder(activeOnly: true), false)).FirstOrDefault();
                    var nextOrder = (highestActiveOrderFaq?.Order ?? 0) + 1;

                    return new ServiceResult(
                        ResultCodeConst.SYS_Success0002,
                        await _faqMsgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                        nextOrder);
                }
                catch (Exception ex)
                {
                    _faqLogger.LogError(ex, "Error getting suggested FAQ order");
                    return new ServiceResult(
                        ResultCodeConst.SYS_Fail0001,
                        await _faqMsgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
                }
            }

            #endregion
        }
    }
