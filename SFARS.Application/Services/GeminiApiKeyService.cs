using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.Params;
using SFARS.Application.Interfaces.Services;

namespace SFARS.Application.Services;

public class GeminiApiKeyService : IGeminiApiKeyService
{
    private readonly IUnitOfWork _uow;
    private readonly ISystemMessageService _msgService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<GeminiApiKeyService> _logger;

    /// <summary>Alert threshold — notify admins when active key count drops to this number or below.</summary>
    private const int LowKeyAlertThreshold = 2;

    public GeminiApiKeyService(
        IUnitOfWork uow,
        ISystemMessageService msgService,
        INotificationService notificationService,
        ILogger<GeminiApiKeyService> logger)
    {
        _uow = uow;
        _msgService = msgService;
        _notificationService = notificationService;
        _logger = logger;
    }

    #region Admin CRUD

    public async Task<IServiceResult> GetAllKeysAsync(BaseSpecParams specParams)
    {
        var repo = _uow.Repository<GeminiApiKey, Guid>();

        var countSpec = new BaseSpecification<GeminiApiKey>(_ => true);

        if (!string.IsNullOrWhiteSpace(specParams.Search))
        {
            countSpec.AddFilter(k => k.Label != null && k.Label.Contains(specParams.Search));
        }

        var totalItems = await repo.CountAsync(countSpec);
        var page = specParams.GetPage();
        var take = specParams.GetTake();

        if (totalItems == 0)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                new PaginatedResultDto<GeminiApiKeyDto>(
                    Enumerable.Empty<GeminiApiKeyDto>(), page, take, 0, 0));
        }

        var spec = new BaseSpecification<GeminiApiKey>(_ => true);

        if (!string.IsNullOrWhiteSpace(specParams.Search))
        {
            spec.AddFilter(k => k.Label != null && k.Label.Contains(specParams.Search));
        }

        spec.ApplyPaging(take, specParams.GetSkip());
        spec.AddOrderByDescending(k => k.CreatedAt);

        var keys = await repo.GetAllWithSpecAsync(spec, tracked: false);
        var dtos = keys.Select(MapToDto).ToList();

        var totalPages = (int)Math.Ceiling((double)totalItems / take);
        var result = new PaginatedResultDto<GeminiApiKeyDto>(dtos, page, take, totalPages, totalItems);

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            result);
    }

    public async Task<IServiceResult> AddKeyAsync(string keyValue, string? label, Guid adminId)
    {
        var repo = _uow.Repository<GeminiApiKey, Guid>();

        // Check duplicate
        var existingSpec = new BaseSpecification<GeminiApiKey>(k => k.KeyValue == keyValue);
        var existing = await repo.GetWithSpecAsync(existingSpec, tracked: false);

        if (existing != null)
        {
            return new ServiceResult(
                ResultCodeConst.GeminiKey_Warning0001,
                await _msgService.GetMessageAsync(ResultCodeConst.GeminiKey_Warning0001));
        }

        var entity = new GeminiApiKey
        {
            KeyValue = keyValue,
            Label = label,
            IsActive = true,
            CreatedBy = adminId
        };

        await repo.AddAsync(entity);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Admin {AdminId} added Gemini API key {KeyId} ({Label})", adminId, entity.Id, label);

        return new ServiceResult(
            ResultCodeConst.GeminiKey_Success0001,
            await _msgService.GetMessageAsync(ResultCodeConst.GeminiKey_Success0001),
            MapToDto(entity));
    }

    public async Task<IServiceResult> ToggleKeyAsync(Guid keyId, Guid adminId)
    {
        var repo = _uow.Repository<GeminiApiKey, Guid>();
        var key = await repo.GetByIdAsync(keyId);

        if (key == null)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0002,
                string.Format(await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002), "API Key"));
        }

        bool newStatus = !key.IsActive;
        key.IsActive = newStatus;
        key.UpdatedAt = DateTime.UtcNow;
        key.UpdatedBy = adminId;

        // If reactivating, also clear exhaustion
        if (newStatus)
        {
            key.IsExhausted = false;
            key.ExhaustedAt = null;
            key.ConsecutiveFailures = 0;
        }

        repo.Update(key);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Admin {AdminId} toggled key {KeyId} to IsActive={IsActive}", adminId, keyId, newStatus);

        // Check if low on keys after disabling
        if (!newStatus)
        {
            await CheckAndAlertLowKeyCountAsync();
        }

        return new ServiceResult(
            ResultCodeConst.GeminiKey_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.GeminiKey_Success0002),
            MapToDto(key));
    }

    public async Task<IServiceResult> DeleteKeyAsync(Guid keyId, Guid adminId)
    {
        var repo = _uow.Repository<GeminiApiKey, Guid>();
        var key = await repo.GetByIdAsync(keyId);

        if (key == null)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0002,
                string.Format(await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002), "API Key"));
        }

        // Prevent deleting the last active key
        var activeCountSpec = new BaseSpecification<GeminiApiKey>(k => k.IsActive && k.Id != keyId);
        var remainingActive = await repo.CountAsync(activeCountSpec);

        if (remainingActive == 0 && key.IsActive)
        {
            return new ServiceResult(
                ResultCodeConst.GeminiKey_Warning0002,
                await _msgService.GetMessageAsync(ResultCodeConst.GeminiKey_Warning0002));
        }

        await repo.DeleteAsync(keyId);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Admin {AdminId} deleted key {KeyId}", adminId, keyId);

        await CheckAndAlertLowKeyCountAsync();

        return new ServiceResult(
            ResultCodeConst.GeminiKey_Success0003,
            await _msgService.GetMessageAsync(ResultCodeConst.GeminiKey_Success0003));
    }

    #endregion

    #region Runtime Key Rotation

    public async Task<GeminiApiKey?> AcquireNextAvailableKeyAsync()
    {
        // LRU strategy: pick the active, non-exhausted key that was used longest ago
        var spec = new BaseSpecification<GeminiApiKey>(k => k.IsActive && !k.IsExhausted);
        spec.AddOrderBy(k => k.LastUsedAt ?? DateTime.MinValue);
        spec.ApplyPaging(1, 0);

        var keys = await _uow.Repository<GeminiApiKey, Guid>().GetAllWithSpecAsync(spec, tracked: false);
        return keys.FirstOrDefault();
    }

    public async Task MarkKeySuccessAsync(Guid keyId)
    {
        var key = await _uow.Repository<GeminiApiKey, Guid>().GetByIdAsync(keyId);
        if (key == null) return;

        key.LastUsedAt = DateTime.UtcNow;
        key.TotalUsageCount++;
        key.ConsecutiveFailures = 0;

        _uow.Repository<GeminiApiKey, Guid>().Update(key);
        await _uow.SaveChangesAsync();
    }

    public async Task MarkKeyExhaustedAsync(Guid keyId)
    {
        var key = await _uow.Repository<GeminiApiKey, Guid>().GetByIdAsync(keyId);
        if (key == null) return;

        key.IsExhausted = true;
        key.ExhaustedAt = DateTime.UtcNow;
        key.ConsecutiveFailures++;

        _uow.Repository<GeminiApiKey, Guid>().Update(key);
        await _uow.SaveChangesAsync();

        _logger.LogWarning("Gemini API key {KeyId} ({Label}) marked exhausted after {Failures} consecutive failures",
            keyId, key.Label, key.ConsecutiveFailures);

        await CheckAndAlertLowKeyCountAsync();
    }

    #endregion

    #region Scheduled Maintenance

    public async Task ResetExhaustedKeysAsync()
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var spec = new BaseSpecification<GeminiApiKey>(k => k.IsExhausted && k.ExhaustedAt != null && k.ExhaustedAt <= cutoff);

        var exhaustedKeys = await _uow.Repository<GeminiApiKey, Guid>().GetAllWithSpecAsync(spec);
        var resetCount = 0;

        foreach (var key in exhaustedKeys)
        {
            key.IsExhausted = false;
            key.ExhaustedAt = null;
            key.ConsecutiveFailures = 0;
            _uow.Repository<GeminiApiKey, Guid>().Update(key);
            resetCount++;
        }

        if (resetCount > 0)
        {
            await _uow.SaveChangesAsync();
            _logger.LogInformation("Auto-reset {Count} exhausted Gemini API keys (exhausted > 24h)", resetCount);
        }
    }

    #endregion

    #region Private Helpers

    private async Task CheckAndAlertLowKeyCountAsync()
    {
        var activeSpec = new BaseSpecification<GeminiApiKey>(k => k.IsActive && !k.IsExhausted);
        var activeCount = await _uow.Repository<GeminiApiKey, Guid>().CountAsync(activeSpec);

        if (activeCount <= LowKeyAlertThreshold)
        {
            _logger.LogWarning("ALERT: Only {Count} active Gemini API keys remaining!", activeCount);

            // Find admin users to notify
            var adminRoleSpec = new BaseSpecification<UserRole>(ur => ur.Role.RoleName == UserTypeConstants.Admin);
            var adminRoles = await _uow.Repository<UserRole, Guid>().GetAllWithSpecAsync(adminRoleSpec, tracked: false);
            var adminIds = adminRoles.Select(ur => ur.UserId).Distinct().ToList();

            if (adminIds.Count > 0)
            {
                var title = "Cảnh báo Gemini API Key";
                var message = $"Chỉ còn {activeCount} key khả dụng. Vui lòng thêm key mới để đảm bảo dịch vụ AI hoạt động liên tục.";

                await _notificationService.SendNotificationsAsync(
                    adminIds, title, message, NotificationType.System);
            }
        }
    }

    private static GeminiApiKeyDto MapToDto(GeminiApiKey k) => new(
        k.Id,
        MaskKey(k.KeyValue),
        k.Label,
        k.IsActive,
        k.IsExhausted,
        k.ExhaustedAt,
        k.LastUsedAt,
        k.TotalUsageCount,
        k.ConsecutiveFailures,
        k.CreatedAt);

    /// <summary>Masks the API key for display: "AIza...4EY"</summary>
    private static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= 7)
            return "****";

        return $"{key[..4]}...{key[^3..]}";
    }

    #endregion
}

/// <summary>DTO projected from GeminiApiKey for admin display — key is always masked.</summary>
public record GeminiApiKeyDto(
    Guid Id,
    string MaskedKey,
    string? Label,
    bool IsActive,
    bool IsExhausted,
    DateTime? ExhaustedAt,
    DateTime? LastUsedAt,
    int TotalUsageCount,
    int ConsecutiveFailures,
    DateTime CreatedAt);