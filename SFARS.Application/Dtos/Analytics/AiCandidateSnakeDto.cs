using System;

namespace SFARS.Application.Dtos.Analytics;

/// <summary>One of the top candidate snakes considered by the AI model</summary>
public class AiCandidateSnakeDto
{
    /// <summary>Unique identifier of the snake species</summary>
    public Guid SnakeId { get; set; }

    /// <summary>Common/Vietnamese name of this candidate snake</summary>
    public string SnakeName { get; set; } = null!;

    /// <summary>Scientific name of this candidate snake</summary>
    public string ScientificName { get; set; } = null!;

    /// <summary>Confidence score for this candidate (0-1 range)</summary>
    public double Confidence { get; set; }

    /// <summary>Whether this was the selected/top candidate</summary>
    public bool IsSelected { get; set; }

    /// <summary>Toxicity level (VeryLow, Low, Moderate, High, VeryHigh)</summary>
    public string ToxicityLevel { get; set; } = null!;

    /// <summary>Toxin group (Neurotoxin, Hemotoxin, Cytotoxin, Myotoxin, Unknown)</summary>
    public string ToxinGroup { get; set; } = null!;
}
