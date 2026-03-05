using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.AiInference;

/// <summary>
/// Crisis-optimized AI inference result for end users
/// </summary>
public class AiInferenceResultDto
{
    public Guid InferenceId { get; set; }
    public SnakeCandidateDto? PrimarySnake { get; set; }
    public List<FirstAidStepDto> FirstAidSteps { get; set; } = new();
    public List<string> Prohibitions { get; set; } = new();

    public List<SnakeCandidateDto> OtherCandidates { get; set; } = new();
    public string? Note { get; set; }

    public DateTime AnalyzedAt { get; set; }

    /// <summary>
    /// Server-authoritative UTC timestamp: the countdown must end by this moment.
    /// FE uses this directly to drive the 10-second cancel timer.
    /// Null if the incident is already past the cancellable window.
    /// </summary>
    public DateTime? CancelDeadline { get; set; }
}

/// <summary>
/// Snake candidate with danger assessment
/// </summary>
public class SnakeCandidateDto
{
    public Guid? SnakeId { get; set; }
    public string ScientificName { get; set; } = null!;
    public string CommonName { get; set; } = null!;
    public double Confidence { get; set; }
    public SnakeRiskLevel ToxicityLevel { get; set; }
    public ToxinGroup ToxinGroup { get; set; }
    public string DangerSummary { get; set; } = null!;
    public string? TypicalSymptoms { get; set; }
}

/// <summary>
/// First aid instruction step
/// </summary>
public class FirstAidStepDto
{
    public int StepOrder { get; set; }
    public string Title { get; set; } = null!;
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
}