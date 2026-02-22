namespace SFARS.Domain.Interfaces.Infrastructure;

/// <summary>
/// Service for Gemini AI to enrich snake detection results with first aid advice
/// </summary>
public interface IGeminiAiService
{
    /// <summary>
    /// Get enriched snake information and first aid recommendations from Gemini
    /// </summary>
    /// <param name="request">Context with YOLO predictions and snake data</param>
    /// <returns>Gemini's analysis with first aid steps</returns>
    Task<GeminiAnalysisResult> AnalyzeSnakeBiteAsync(GeminiAnalysisRequest request);
}

/// <summary>
/// Request payload for Gemini analysis
/// </summary>
public record GeminiAnalysisRequest(
    string PrimarySnakeName,
    string PrimarySnakeScientificName,
    string ToxicityLevel,
    string ToxinGroup,
    float Confidence,
    List<AlternativeSnake> OtherCandidates
);

public record AlternativeSnake(
    string CommonName,
    string ScientificName,
    float Confidence
);

/// <summary>
/// Gemini's structured response
/// </summary>
public record GeminiAnalysisResult(
    string DangerSummary,
    List<FirstAidStep> FirstAidSteps,
    string AiNote                   // Additional context/warnings
);

public record FirstAidStep(
    int StepOrder,
    string Title,
    string Content                  // Short actionable instruction
);