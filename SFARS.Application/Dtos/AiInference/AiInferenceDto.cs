using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.AiInference;

/// <summary>
/// Crisis-optimized AI inference result for end users
/// </summary>
public class AiInferenceResultDto
{
    public Guid InferenceId { get; set; }
    public SnakeCandidateDto PrimarySnake { get; set; } = null!;
    public List<FirstAidStepDto> FirstAidSteps { get; set; } = new();
    public List<SnakeCandidateDto> OtherCandidates { get; set; } = new();
    public string AiNote { get; set; } = null!;
    public DateTime AnalyzedAt { get; set; }
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
    public string DangerSummary { get; set; } = null!; // "⚠️ CỰC KỲ NGUY HIỂM"
}

/// <summary>
/// First aid instruction step
/// </summary>
public class FirstAidStepDto
{
    public int StepOrder { get; set; }
    public string Title { get; set; } = null!;        // With emoji: "🚑 GỌI CẤP CỨU"
    public string Content { get; set; } = null!;      // Short actionable text
    public string? ImageUrl { get; set; }             // Optional illustration
}