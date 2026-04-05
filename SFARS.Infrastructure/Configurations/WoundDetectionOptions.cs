namespace SFARS.Infrastructure.Configurations;

/// <summary>
/// Configuration for 2-Stage Wound Analysis Pipeline.
/// Stage 1: YOLOv8n detection model (locate wound bounding box).
/// Stage 2: YOLOv8n-cls classification model (Snake Bite vs Non Snake Bite).
/// </summary>
public class WoundDetectionOptions
{
    public const string SectionName = "WoundDetection";

    // ──── Stage 1: Detection (YOLOv8n) ────

    /// <summary>
    /// Path to the wound detection ONNX model (YOLOv8n).
    /// </summary>
    public string DetectionModelPath { get; set; } = "wwwroot/models/wound_detector.onnx";

    /// <summary>
    /// Input image size for YOLO detection model (square).
    /// Default: 640 matching YOLOv8n training pipeline.
    /// </summary>
    public int DetectionInputSize { get; set; } = 640;

    /// <summary>
    /// Minimum confidence threshold for wound detection.
    /// Set low (0.15) to maximize recall — in medical context, missing a wound is worse than a false positive.
    /// Matches the training evaluation: conf=0.15.
    /// </summary>
    public float DetectionConfidenceThreshold { get; set; } = 0.15f;

    /// <summary>
    /// IoU threshold for Non-Maximum Suppression during wound detection.
    /// Matches the training evaluation: iou=0.3.
    /// </summary>
    public float DetectionNmsIouThreshold { get; set; } = 0.30f;

    /// <summary>
    /// Margin ratio to expand the bounding box before cropping for classification.
    /// 0.15 = 15% expansion on each side for better context.
    /// </summary>
    public float MarginRatio { get; set; } = 0.15f;

    // ──── Stage 2: Classification (YOLOv8n-cls) ────

    /// <summary>
    /// Path to the wound classification ONNX model (YOLOv8n-cls).
    /// </summary>
    public string ClassificationModelPath { get; set; } = "wwwroot/models/wound_classifier.onnx";

    /// <summary>
    /// Input image size for classification model (square).
    /// Default: 224 matching YOLOv8n-cls training pipeline (imgsz=224).
    /// </summary>
    public int ClassificationInputSize { get; set; } = 224;

    /// <summary>
    /// Confidence threshold to classify as a snake bite.
    /// Default is 0.50 (50%).
    /// </summary>
    public float ClassificationThreshold { get; set; } = 0.50f;

    // ──── Pipeline Metadata ────

    /// <summary>
    /// Display name for the wound analysis pipeline.
    /// </summary>
    public string ModelName { get; set; } = "wound-cascade-v1";

    /// <summary>
    /// Version identifier for audit trail.
    /// </summary>
    public string ModelVersion { get; set; } = "1.0.0";
}