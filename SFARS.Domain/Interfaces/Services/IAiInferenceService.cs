using SFARS.Domain.Common.Enum;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services;

/// <summary>
/// Service for AI-powered snake detection and analysis
/// </summary>
public interface IAiInferenceService
{
    /// <summary>
    /// Upload media + run AI inference in a single flow.
    /// Uploads to cloud storage, runs YOLO on original stream, queries DB for first-aid,
    /// creates AI chat initial message, and saves all in 1 transaction.
    /// </summary>
    Task<IServiceResult> AnalyzeAsync(
        Guid userId,
        Guid incidentId,
        Stream? imageStream,
        string? fileName,
        string? contentType,
        long? fileSize);

    /// <summary>
    /// Standalone snake identification. Does not upload image or save to DB.
    /// Fast response for Pokedex-like features.
    /// </summary>
    Task<IServiceResult> IdentifySnakeAsync(Stream? imageStream, string? contentType, long? fileSize);

    /// <summary>
    /// Standalone wound classification. Does not upload image or save to DB.
    /// Fast response for Wound Identification feature.
    /// </summary>
    Task<IServiceResult> ClassifyWoundAsync(Stream? imageStream, string? contentType, long? fileSize);
}