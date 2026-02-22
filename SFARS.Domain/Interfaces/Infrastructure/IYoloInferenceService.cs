namespace SFARS.Domain.Interfaces.Infrastructure;

/// <summary>
/// Service for YOLO model inference to detect snakes in images
/// </summary>
public interface IYoloInferenceService
{
    /// <summary>
    /// Run inference on an image to detect snakes
    /// </summary>
    /// <param name="imageStream">Image stream to analyze</param>
    /// <param name="topK">Number of top predictions to return (default: 3)</param>
    /// <returns>List of predictions with snake class names and confidence scores</returns>
    Task<IReadOnlyList<YoloPrediction>> InferAsync(Stream imageStream, int topK = 3);
}

/// <summary>
/// Represents a single YOLO prediction result
/// </summary>
public record YoloPrediction(
    string ClassName,      // e.g., "naja_kaouthia"
    float Confidence,      // 0.0 - 1.0
    int ClassIndex
);