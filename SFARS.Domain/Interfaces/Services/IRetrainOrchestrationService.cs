using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services;

/// <summary>
/// Orchestrates the automated "Human-in-the-Loop" MLOps pipeline.
/// </summary>
public interface IRetrainOrchestrationService
{
    /// <summary>
    /// Triggers the full snake species MLOps retrain pipeline asynchronously.
    /// </summary>
    Task<IServiceResult> TriggerRetrainAsync(DateTime? since = null);

    /// <summary>
    /// Triggers the wound classification MLOps retrain pipeline asynchronously.
    /// </summary>
    Task<IServiceResult> TriggerWoundRetrainAsync(DateTime? since = null);
    
    /// <summary>
    /// Gets the most recent retrain history records.
    /// </summary>
    Task<IServiceResult> GetRetrainHistoryAsync(int count = 10);
}