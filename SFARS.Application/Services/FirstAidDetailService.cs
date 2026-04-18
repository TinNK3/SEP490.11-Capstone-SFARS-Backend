using MapsterMapper;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.FirstAidDetail;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.FirstAidDetails;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Application.Services;

public class FirstAidDetailService : IFirstAidDetailService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ISystemMessageService _msgService;
    private readonly ILogger<FirstAidDetailService> _logger;
    private readonly IFileStorageService _storageService;

    public FirstAidDetailService(
        IUnitOfWork uow,
        IMapper mapper,
        ISystemMessageService msgService,
        ILogger<FirstAidDetailService> logger,
        IFileStorageService storageService)
    {
        _uow = uow;
        _mapper = mapper;
        _msgService = msgService;
        _logger = logger;
        _storageService = storageService;
    }

    /// <inheritdoc />
    public async Task<IServiceResult> GetAllGroupedAsync(string? toxinGroup)
    {
        var repo = _uow.Repository<FirstAidDetail, Guid>();

        ToxinGroup? parsedToxin = null;
        if (!string.IsNullOrWhiteSpace(toxinGroup))
        {
            if (Enum.TryParse<ToxinGroup>(toxinGroup, ignoreCase: true, out var t))
            {
                parsedToxin = t;
            }
            else
            {
                return new ServiceResult(
                    ResultCodeConst.FirstAid_Warning0003,
                    await _msgService.GetMessageAsync(ResultCodeConst.FirstAid_Warning0003));
            }
        }

        var spec = FirstAidDetailSpecification.AllGrouped(parsedToxin);

        var items = await repo.GetAllWithSpecAsync(spec, tracked: false);
        
        if (!items.Any())
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0004,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004),
                new Dictionary<string, List<FirstAidDetailDto>>());
        }

        var dtos = _mapper.Map<IEnumerable<FirstAidDetailDto>>(items);

        // Group by ToxinGroup -> List of Steps
        var grouped = dtos
            .GroupBy(d => d.ToxinGroup)
            .ToDictionary(
                g => g.Key, 
                g => g.OrderBy(d => d.StepOrder).ToList()
            );

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            grouped);
    }

    /// <inheritdoc />
    public async Task<IServiceResult> GetByIdAsync(Guid id)
    {
        var entity = await _uow.Repository<FirstAidDetail, Guid>().GetByIdAsync(id);
        if (entity == null)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0004,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
        }

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            _mapper.Map<FirstAidDetailDto>(entity));
    }

    /// <inheritdoc />
    public async Task<IServiceResult> CreateAsync<TDto>(Guid adminId, TDto dto, FirstAidImageUploadInfo? imageInfo = null) where TDto : class
    {
        var request = dto as FirstAidDetailDto;
        if (request == null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));

        var repo = _uow.Repository<FirstAidDetail, Guid>();

        Enum.TryParse<ToxinGroup>(request.ToxinGroup, ignoreCase: true, out var parsedToxin);


        if (!Enum.TryParse<SystemLanguage>(request.LanguageCode, ignoreCase: true, out var parsedLang))
        {
            return new ServiceResult(
                ResultCodeConst.FirstAid_Warning0004,
                await _msgService.GetMessageAsync(ResultCodeConst.FirstAid_Warning0004));
        }

        // Validate SnakeId if provided
        if (request.SnakeId.HasValue)
        {
            var snake = await _uow.Repository<Snake, Guid>().GetByIdAsync(request.SnakeId.Value);
            if (snake == null)
            {
                return new ServiceResult(
                    ResultCodeConst.FirstAid_Warning0005,
                    await _msgService.GetMessageAsync(ResultCodeConst.FirstAid_Warning0005));
            }
        }

        // Check unique constraint: ToxinGroup + LanguageCode + StepOrder + SnakeId
        var duplicateSpec = new BaseSpecification<FirstAidDetail>(e =>
            e.ToxinGroup == parsedToxin &&
            e.LanguageCode == parsedLang &&
            e.StepOrder == request.StepOrder &&
            e.SnakeId == request.SnakeId);
        var duplicate = await repo.GetWithSpecAsync(duplicateSpec, tracked: false);
        if (duplicate != null)
        {
            return new ServiceResult(
                ResultCodeConst.FirstAid_Warning0002,
                await _msgService.GetMessageAsync(ResultCodeConst.FirstAid_Warning0002));
        }

        // Handle Image Upload within Service logic
        string? finalImageUrl = request.ImageUrl;
        if (imageInfo != null)
        {
            var uploadResult = await _storageService.UploadAsync(
                imageInfo.Stream, 
                imageInfo.FileName, 
                "first_aid", 
                imageInfo.ContentType);
            finalImageUrl = uploadResult.Url;
        }

        var entity = new FirstAidDetail
        {
            ToxinGroup = parsedToxin,
            SnakeId = request.SnakeId,
            StepOrder = request.StepOrder,
            Title = request.Title.Trim(),
            ContentMarkdown = request.ContentMarkdown?.Trim(),
            ImageUrl = finalImageUrl?.Trim(),
            LanguageCode = parsedLang,
            CreatedBy = adminId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repo.AddAsync(entity);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Admin {AdminId} created FirstAidDetail {Id} [{ToxinGroup} Step {Order}]",
            adminId, entity.Id, parsedToxin, request.StepOrder);

        return new ServiceResult(
            ResultCodeConst.FirstAid_Success0001,
            await _msgService.GetMessageAsync(ResultCodeConst.FirstAid_Success0001),
            _mapper.Map<FirstAidDetailDto>(entity));
    }

    /// <inheritdoc />
    public async Task<IServiceResult> UpdateAsync<TDto>(Guid id, Guid adminId, TDto dto, FirstAidImageUploadInfo? imageInfo = null) where TDto : class
    {
        var request = dto as FirstAidDetailDto;
        if (request == null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));

        var repo = _uow.Repository<FirstAidDetail, Guid>();
        var entity = await repo.GetByIdAsync(id);

        if (entity == null)
        {
            return new ServiceResult(
                ResultCodeConst.FirstAid_Warning0001,
                await _msgService.GetMessageAsync(ResultCodeConst.FirstAid_Warning0001));
        }

        Enum.TryParse<ToxinGroup>(request.ToxinGroup, ignoreCase: true, out var parsedToxin);


        if (!Enum.TryParse<SystemLanguage>(request.LanguageCode, ignoreCase: true, out var parsedLang))
        {
            return new ServiceResult(
                ResultCodeConst.FirstAid_Warning0004,
                await _msgService.GetMessageAsync(ResultCodeConst.FirstAid_Warning0004));
        }

        // Validate SnakeId if provided
        if (request.SnakeId.HasValue)
        {
            var snake = await _uow.Repository<Snake, Guid>().GetByIdAsync(request.SnakeId.Value);
            if (snake == null)
            {
                return new ServiceResult(
                    ResultCodeConst.FirstAid_Warning0005,
                    await _msgService.GetMessageAsync(ResultCodeConst.FirstAid_Warning0005));
            }
        }

        // Check unique constraint (exclude self)
        var duplicateSpec = new BaseSpecification<FirstAidDetail>(e =>
            e.Id != id &&
            e.ToxinGroup == parsedToxin &&
            e.LanguageCode == parsedLang &&
            e.StepOrder == request.StepOrder &&
            e.SnakeId == request.SnakeId);
        var duplicate = await repo.GetWithSpecAsync(duplicateSpec, tracked: false);
        if (duplicate != null)
        {
            return new ServiceResult(
                ResultCodeConst.FirstAid_Warning0002,
                await _msgService.GetMessageAsync(ResultCodeConst.FirstAid_Warning0002));
        }

        // Handle Image Upload (Optional Update)
        if (imageInfo != null)
        {
            // Delete old image if it exists
            if (!string.IsNullOrWhiteSpace(entity.ImageUrl))
            {
                await _storageService.DeleteByUrlAsync(entity.ImageUrl);
            }

            var uploadResult = await _storageService.UploadAsync(
                imageInfo.Stream, 
                imageInfo.FileName, 
                "first_aid", 
                imageInfo.ContentType);
            entity.ImageUrl = uploadResult.Url;
        }

        entity.ToxinGroup = parsedToxin;
        entity.SnakeId = request.SnakeId;
        entity.StepOrder = request.StepOrder;
        entity.Title = request.Title.Trim();
        entity.ContentMarkdown = request.ContentMarkdown?.Trim();
        entity.LanguageCode = parsedLang;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = adminId;

        repo.Update(entity);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Admin {AdminId} updated FirstAidDetail {Id}", adminId, id);

        return new ServiceResult(
            ResultCodeConst.FirstAid_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.FirstAid_Success0002),
            _mapper.Map<FirstAidDetailDto>(entity));
    }

    /// <inheritdoc />
    public async Task<IServiceResult> DeleteAsync(Guid id, Guid adminId)
    {
        var repo = _uow.Repository<FirstAidDetail, Guid>();
        var entity = await repo.GetByIdAsync(id);

        if (entity == null)
        {
            return new ServiceResult(
                ResultCodeConst.FirstAid_Warning0001,
                await _msgService.GetMessageAsync(ResultCodeConst.FirstAid_Warning0001));
        }

        await repo.DeleteAsync(id);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Admin {AdminId} deleted FirstAidDetail {Id}", adminId, id);

        return new ServiceResult(
            ResultCodeConst.FirstAid_Success0003,
            await _msgService.GetMessageAsync(ResultCodeConst.FirstAid_Success0003));
    }

}