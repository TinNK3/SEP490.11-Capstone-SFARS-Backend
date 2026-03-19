namespace SFARS.Infrastructure.Configurations;

/// <summary>
/// Strongly-typed configuration for the MLOps Retrain Pipeline.
/// Bound from the "Mlops" section in appsettings.json.
/// </summary>
public class MlopsOptions
{
    /// <summary>
    /// Path or command name of the Python executable. Default: "python".
    /// </summary>
    public string PythonExecutable { get; set; } = "python";

    /// <summary>
    /// API Key used by MLOps Python scripts to authenticate with the export endpoint.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Number of new verified samples required to automatically trigger retraining.
    /// </summary>
    public int AutoRetrainThreshold { get; set; } = 50;

    /// <summary>
    /// Relative path from the solution root to the MLOps scripts directory.
    /// Default: "scripts/mlops".
    /// </summary>
    public string ScriptsRelativePath { get; set; } = "scripts/mlops";

    /// <summary>
    /// Name of the main pipeline script to execute.
    /// </summary>
    public string PipelineScriptName { get; set; } = "pipeline.py";

    /// <summary>
    /// Maximum time in minutes to wait for the Python pipeline to complete.
    /// </summary>
    public int PipelineTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum number of characters from stderr/stdout to include in error messages.
    /// </summary>
    public int MaxErrorLogLength { get; set; } = 500;

    /// <summary>
    /// Pre-trained YOLO model file name used as the base for transfer learning.
    /// This value is passed to trainer.py via the PRETRAINED_MODEL env variable.
    /// </summary>
    public string PretrainedModelName { get; set; } = "yolov8s-cls.pt";
}