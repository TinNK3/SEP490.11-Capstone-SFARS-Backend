namespace SFARS.Domain.Interfaces.Infrastructure;

/// <summary>
/// 2-Stage wound analysis pipeline:
/// Stage 1 — YOLOv8n detection: locate bite wound region &amp; bounding box.
/// Stage 2 — YOLOv8n-cls classification: classify wound as Snake Bite / Non Snake Bite.
/// Mirrors the snake identification pipeline (ISnakeDetectionService → ISpeciesClassificationService).
/// </summary>
public interface IWoundDetectionService
{
    /// <summary>
    /// Stage 1: Detect bite wound in image and return bounding box coordinates.
    /// </summary>
    /// <param name="imageBytes">Raw image bytes (JPEG/PNG).</param>
    /// <returns>Detection result with confidence and bounding box in pixel coordinates.</returns>
    Task<WoundDetectionResult> DetectWoundAsync(byte[] imageBytes);

    /// <summary>
    /// Stage 2: Classify a cropped wound image as Snake Bite or Non Snake Bite.
    /// Input should be a pre-cropped image from Stage 1.
    /// </summary>
    /// <param name="croppedImageBytes">Pre-cropped wound image bytes.</param>
    /// <returns>Classification result with IsSnakeBite flag and confidence.</returns>
    Task<WoundClassificationResult> ClassifyWoundAsync(byte[] croppedImageBytes);

    /// <summary>
    /// Hot-reload the wound detection ONNX model from disk without app restart.
    /// </summary>
    Task<bool> ReloadDetectionModelAsync(string? newModelPath = null);

    /// <summary>
    /// Hot-reload the wound classification ONNX model from disk without app restart.
    /// </summary>
    Task<bool> ReloadClassificationModelAsync(string? newModelPath = null);
}

/// <summary>
/// Result from YOLOv8n wound detection — bounding box in pixel coordinates.
/// </summary>
public record WoundDetectionResult(
    bool IsDetected,
    float Confidence,
    BoundingBox? Box
);

/// <summary>
/// Result from YOLOv8n-cls wound classification.
/// </summary>
public record WoundClassificationResult(
    bool IsSnakeBite,
    float Confidence
);