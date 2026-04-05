using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services;

/// <summary>
/// Service for exporting human-verified data for AI model retraining.
/// </summary>
public interface IDataSetExportService
{
    /// <summary>
    /// Exports snake-photo AI inferences that have been reviewed and corrected/confirmed by rescuers.
    /// This provides high-quality ground truth data for snake species retraining.
    /// </summary>
    Task<IServiceResult> ExportTrainingDataAsync(DateTime? since = null);

    /// <summary>
    /// Exports wound-photo AI inferences that have been reviewed by rescuers.
    /// Provides ground truth data (Snake_Bite / Non_Snake_Bite) for wound classifier retraining.
    /// </summary>
    Task<IServiceResult> ExportWoundTrainingDataAsync(DateTime? since = null);
}