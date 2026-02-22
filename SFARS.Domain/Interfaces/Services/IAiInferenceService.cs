using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services;

/// <summary>
/// Service for AI-powered snake detection and analysis
/// </summary>
public interface IAiInferenceService
{
    /// <summary>
    /// Create AI inference for an incident based on uploaded media
    /// </summary>
    /// <param name="userId">User requesting the inference (must be incident owner)</param>
    /// <param name="incidentId">Incident ID</param>
    /// <param name="incidentMediaId">Media ID to analyze (must belong to incident)</param>
    /// <returns>AI inference result with snake detection and first aid</returns>
    Task<IServiceResult> CreateInferenceAsync(
        Guid userId,
        Guid incidentId,
        Guid incidentMediaId);
}