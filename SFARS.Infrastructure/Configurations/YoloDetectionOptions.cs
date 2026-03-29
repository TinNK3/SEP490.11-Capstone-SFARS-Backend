namespace SFARS.Infrastructure.Configurations;

/// <summary>
/// Configuration for YOLO object detection model (Stage 1 of 2-stage pipeline).
/// </summary>
public class YoloDetectionOptions
{
    public const string SectionName = "YoloDetection";

    public string ModelPath { get; set; } = "wwwroot/models/yolov8_snake_detector.onnx";

    /// <summary>
    /// Input image size for YOLO model (square).
    /// </summary>
    public int InputSize { get; set; } = 640;

    /// <summary>
    /// Minimum confidence threshold for YOLO detection.
    /// Detections below this are discarded.
    /// </summary>
    public float ConfidenceThreshold { get; set; } = 0.25f;

    /// <summary>
    /// IoU threshold for Non-Maximum Suppression.
    /// </summary>
    public float NmsIouThreshold { get; set; } = 0.45f;

    /// <summary>
    /// Margin ratio to expand the bounding box for better classification.
    /// 0.15 = 15% expansion on each side.
    /// </summary>
    public float MarginRatio { get; set; } = 0.15f;
}