namespace SFARS.Domain.Interfaces.Infrastructure;

/// <summary>
/// Service for snake detection using object detection model (YOLOv8).
/// Stage 1 of the 2-stage pipeline: detect snake presence and location.
/// </summary>
public interface ISnakeDetectionService
{
    /// <summary>
    /// Detect snake in image and return bounding box coordinates.
    /// </summary>
    /// <param name="imageBytes">Raw image bytes (JPEG/PNG).</param>
    /// <returns>Detection result with confidence and bounding box in pixel coordinates.</returns>
    Task<SnakeDetectionResult> DetectAsync(byte[] imageBytes);

    /// <summary>
    /// Hot-reload the detection ONNX model from disk without app restart.
    /// </summary>
    Task<bool> ReloadModelAsync(string? newModelPath = null);
}

/// <summary>
/// Result from YOLO snake detection — bounding box in pixel coordinates.
/// </summary>
public record SnakeDetectionResult(
    bool IsDetected,
    float Confidence,
    BoundingBox? Box
);

/// <summary>
/// Bounding box in absolute pixel coordinates.
/// </summary>
public record BoundingBox(int XMin, int YMin, int XMax, int YMax);