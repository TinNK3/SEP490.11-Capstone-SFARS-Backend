using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services;

/// <summary>
/// Service for exporting human-verified data for AI model retraining.
/// </summary>
public interface IDataSetExportService
{
    /// <summary>
    /// Exports all AI inferences that have been reviewed and corrected/confirmed by rescuers.
    /// This provides high-quality ground truth data for YOLO retraining.
    /// </summary>
    Task<IServiceResult> ExportTrainingDataAsync(DateTime? since = null);
}