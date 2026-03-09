using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Common;

/// <summary>
/// Application-level constants for AI inference logic.
/// These are domain values, NOT system message keys.
/// </summary>
public static class AiInferenceConstants
{
    #region YOLO Model

    /// <summary>
    /// Default YOLO model identifier stored in AiInference records.
    /// Matches the ModelName seeded from YoloModelOptions by convention.
    /// </summary>
    public const string ModelName = "snake-cls-v1";

    public const string ModelVersion = "1.0.0";

    public const int DefaultTopK = 3;

    /// <summary>Sentinel model name when no inference was performed (skip photo scenario).</summary>
    public const string SkippedModelName = "None";
    public const string SkippedModelVersion = "N/A";

    #endregion

    #region Decision Rules

    public const string DecisionRuleTop1 = "Top1";
    public const string DecisionRuleSkipped = "Skipped";

    public const string UnknownSnake = "Unknown";

    #endregion

    #region Danger Labels (display strings for SnakeRiskLevel)

    public static string GetDangerLabel(SnakeRiskLevel level) => level switch
    {
        SnakeRiskLevel.Deadly => "CỰC KỲ NGUY HIỂM",
        SnakeRiskLevel.HighlyVenomous => "NGUY HIỂM",
        SnakeRiskLevel.MildlyVenomous => "ÍT ĐỘC",
        SnakeRiskLevel.NonVenomous => "KHÔNG ĐỘC",
        _ => "CẦN KIỂM TRA"
    };

    #endregion
}