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

    // Snake Species Pipeline
    /// <summary>
    /// Number of new verified snake samples required to automatically trigger retraining.
    /// </summary>
    public int AutoRetrainThreshold { get; set; } = 50;

    /// <summary>
    /// Relative path from the solution root to the snake MLOps scripts directory.
    /// </summary>
    public string ScriptsRelativePath { get; set; } = "scripts/mlops/snake";

    /// <summary>
    /// Name of the main snake pipeline script to execute.
    /// </summary>
    public string PipelineScriptName { get; set; } = "snake_pipeline.py";

    // Wound Classification Pipeline
    /// <summary>
    /// Number of new verified wound samples required to automatically trigger retraining.
    /// </summary>
    public int WoundAutoRetrainThreshold { get; set; } = 20;

    /// <summary>
    /// Relative path from the solution root to the wound MLOps scripts directory.
    /// </summary>
    public string WoundScriptsRelativePath { get; set; } = "scripts/mlops/wound";

    /// <summary>
    /// Name of the main wound pipeline script to execute.
    /// </summary>
    public string WoundPipelineScriptName { get; set; } = "wound_pipeline.py";

    // Shared Settings
    /// <summary>
    /// Maximum time in minutes to wait for the Python pipeline to complete.
    /// </summary>
    public int PipelineTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum number of characters from stderr/stdout to include in error messages.
    /// </summary>
    public int MaxErrorLogLength { get; set; } = 500;
}