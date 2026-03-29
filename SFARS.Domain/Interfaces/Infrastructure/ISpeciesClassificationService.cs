namespace SFARS.Domain.Interfaces.Infrastructure;

/// <summary>
/// Service for classification model inference to classify snake species.
/// </summary>
public interface ISpeciesClassificationService
{
    /// <summary>
    /// Run species-only classification.
    /// Called after Gemini Vision confirms the image contains a snake.
    /// </summary>
    /// <param name="imageStream">Image stream to classify.</param>
    /// <param name="topK">Number of top species predictions to return (default: 3).</param>
    /// <returns>Ranked list of species predictions.</returns>
    Task<IReadOnlyList<SpeciesPrediction>> InferSpeciesOnlyAsync(Stream imageStream, int topK = 3);
    
    /// <summary>
    /// Hot-reload the species ONNX model from disk without app restart.
    /// </summary>
    Task<bool> ReloadSpeciesModelAsync(string? newModelPath = null);
}



/// <summary>
/// Represents a single classification prediction result
/// </summary>
public record SpeciesPrediction(
    string ClassName,      // e.g., "naja_kaouthia"
    float Confidence,      // 0.0 - 1.0
    int ClassIndex
);