namespace SFARS.Infrastructure.Configurations;

/// <summary>
/// Configuration for YOLO model inference
/// </summary>
public class YoloModelOptions
{
    public string ModelPath { get; set; } = "wwwroot/models/best.onnx";
    public string ModelName { get; set; } = "snake-cls-v1";
    public string ModelVersion { get; set; } = "1.0.0";
    public int InputWidth { get; set; } = 224;
    public int InputHeight { get; set; } = 224;
    public int TopK { get; set; } = 3;
    
    /// <summary>
    /// Mapping from YOLO class index to Snake scientific name
    /// Format: "0:naja_kaouthia,1:ophiophagus_hannah,2:..."
    /// </summary>
    public string ClassMapping { get; set; } = string.Empty;
}