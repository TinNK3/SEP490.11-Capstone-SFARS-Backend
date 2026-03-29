namespace SFARS.Infrastructure.Configurations;

/// <summary>
/// Configuration for classification model inference
/// </summary>
public class ClassificationModelOptions
{
    public const string SectionName = "ClassificationModel";
    public string ModelPath { get; set; } = "wwwroot/models/best.onnx";
    public string ModelName { get; set; } = "snake-cls-v1";
    public string ModelVersion { get; set; } = "1.0.0";
    

    public string SpeciesInputName { get; set; } = "images";
    public int SpeciesInputWidth { get; set; } = 320;
    public int SpeciesInputHeight { get; set; } = 320;

    public int TopK { get; set; } = 3;
    
    /// <summary>
    /// Mapping from YOLO class index to Snake scientific name
    /// Format: "0:naja_kaouthia,1:ophiophagus_hannah,2:..."
    /// </summary>
    public string ClassMapping { get; set; } = string.Empty;
}