using System;

namespace SFARS.Application.Dtos.Analytics;

/// <summary>AI snake identification result and model details</summary>
public class SnakeAiIdentificationDto
{
    /// <summary>Name of the AI model used (e.g., "snake-cls-v1")</summary>
    public string? ModelName { get; set; }

    /// <summary>Version of the AI model (e.g., "1.0.0")</summary>
    public string? ModelVersion { get; set; }

    /// <summary>Confidence score of the selected snake (0-1 range)</summary>
    public double? SelectedConfidence { get; set; }

    /// <summary>Number of top candidates considered (typically 3)</summary>
    public int TopK { get; set; }

    /// <summary>Decision rule applied (e.g., "Top1>=0.7", "Top1Only")</summary>
    public string? DecisionRule { get; set; }

    /// <summary>When the AI inference was created (UTC)</summary>
    public DateTime CreatedAt { get; set; }
}
