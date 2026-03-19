namespace SFARS.Domain.Interfaces.Infrastructure;

/// <summary>
/// Service for YOLO model inference to detect and classify snake species.
/// </summary>
public interface IYoloInferenceService
{


    /// <summary>
    /// Run species-only classification (skip binary snake/not-snake detection).
    /// Called after Gemini Vision confirms the image contains a snake.
    /// </summary>
    /// <param name="imageStream">Image stream to classify.</param>
    /// <param name="topK">Number of top species predictions to return (default: 3).</param>
    /// <returns>Ranked list of species predictions.</returns>
    Task<IReadOnlyList<YoloPrediction>> InferSpeciesOnlyAsync(Stream imageStream, int topK = 3);
    
    /// <summary>
    /// Hot-reload the species ONNX model from disk without app restart.
    /// Thread-safe: uses read-write lock to prevent inference during reload.
    /// </summary>
    Task<bool> ReloadSpeciesModelAsync(string? newModelPath = null);
}



/// <summary>
/// Represents a single YOLO prediction result
/// </summary>
public record YoloPrediction(
    string ClassName,      // e.g., "naja_kaouthia"
    float Confidence,      // 0.0 - 1.0
    int ClassIndex
);