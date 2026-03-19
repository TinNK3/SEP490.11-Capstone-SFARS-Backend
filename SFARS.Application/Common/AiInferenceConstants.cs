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
    /// Pipeline model identifier: Gemini Vision (binary) → YOLO Species (classification).
    /// </summary>
    public const string ModelName = "gemini-yolo-cascade-v1";

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

    /// <summary>
    /// Minimum confidence to display snake species to users.
    /// Below this threshold, candidates are hidden to prevent misinformation.
    /// </summary>
    public const double ConfidenceDisplayThreshold = 0.60;

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

    #region Auto-Priority

    /// <summary>
    /// Determines the incident priority level based on AI snake detection results.
    /// Called after AnalyzeAsync and after SubmitReviewAsync (if rescuer corrects the species).
    /// </summary>
    public static SeverityLevel DetermineIncidentPriority(
        bool isSkip,
        bool isLowConfidence,
        SnakeRiskLevel? toxicityLevel)
    {
        // Skip photo or low confidence → assume danger
        if (isSkip || isLowConfidence)
            return SeverityLevel.High;

        return toxicityLevel switch
        {
            SnakeRiskLevel.Deadly => SeverityLevel.Critical,
            SnakeRiskLevel.HighlyVenomous => SeverityLevel.Critical,
            SnakeRiskLevel.MildlyVenomous => SeverityLevel.Medium,
            SnakeRiskLevel.NonVenomous => SeverityLevel.Low,
            _ => SeverityLevel.High  // Unknown or null → be cautious
        };
    }

    #endregion
}