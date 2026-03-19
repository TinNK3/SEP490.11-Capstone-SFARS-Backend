namespace SFARS.Domain.Interfaces.Infrastructure;

/// <summary>
/// Service for YOLO model inference to detect snakes in images
/// </summary>
public interface IYoloInferenceService
{
    /// <summary>
    /// Run inference on an image using cascaded models (Binary -> Species)
    /// </summary>
    /// <param name="imageStream">Image stream to analyze</param>
    /// <param name="topK">Number of top species predictions to return if it's a snake (default: 3)</param>
    /// <returns>Pipeline result containing IsSnake flag and species predictions</returns>
    Task<YoloPipelineResult> InferCascadedAsync(Stream imageStream, int topK = 3);
}

/// <summary>
/// Represents the result of the entire cascaded YOLO pipeline
/// </summary>
public record YoloPipelineResult(
    bool IsSnake,
    float BinaryConfidence,
    IReadOnlyList<YoloPrediction> SpeciesPredictions
);

/// <summary>
/// Represents a single YOLO prediction result
/// </summary>
public record YoloPrediction(
    string ClassName,      // e.g., "naja_kaouthia"
    float Confidence,      // 0.0 - 1.0
    int ClassIndex
);