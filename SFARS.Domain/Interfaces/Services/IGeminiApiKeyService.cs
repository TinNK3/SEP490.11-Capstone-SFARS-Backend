using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Interfaces.Services;

/// <summary>
/// Manages Gemini API keys: admin CRUD + runtime key rotation.
/// </summary>
public interface IGeminiApiKeyService
{
    // Admin CRUD
    Task<IServiceResult> GetAllKeysAsync(BaseSpecParams specParams);
    Task<IServiceResult> AddKeyAsync(string keyValue, string? label, Guid adminId);
    Task<IServiceResult> ToggleKeyAsync(Guid keyId, Guid adminId);
    Task<IServiceResult> DeleteKeyAsync(Guid keyId, Guid adminId);

    // Runtime (called by GeminiAiService)
    Task<GeminiApiKey?> AcquireNextAvailableKeyAsync();
    Task MarkKeySuccessAsync(Guid keyId);
    Task MarkKeyExhaustedAsync(Guid keyId);

    // Scheduled Maintenance
    Task ResetExhaustedKeysAsync();
}