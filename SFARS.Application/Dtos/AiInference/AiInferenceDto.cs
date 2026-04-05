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
    public List<FirstAidStepDto> Prohibitions { get; set; } = new();

    /// <summary>
    /// Populated only if the user uploads a Wound Photo (MediaType = BiteWoundPhoto).
    /// If this is populated, PrimarySnake will be null.
    /// </summary>
    public WoundAnalysisDto? WoundAnalysis { get; set; }

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
/// Wound detection result for SOS case
/// </summary>
public class WoundAnalysisDto
{
    /// <summary>
    /// Whether the detection model found a wound region in the image.
    /// False means the image likely does not contain a visible wound.
    /// </summary>
    public bool IsWoundDetected { get; set; }

    /// <summary>
    /// Whether the classification model identified the wound as a snake bite.
    /// Only meaningful when IsWoundDetected is true.
    /// </summary>
    public bool IsSnakeBite { get; set; }

    /// <summary>
    /// Confidence score from the classification model (0.0 - 1.0).
    /// </summary>
    public double Confidence { get; set; }
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