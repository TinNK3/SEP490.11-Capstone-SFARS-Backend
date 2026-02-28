namespace SFARS.Domain.Interfaces.Infrastructure;

/// <summary>
/// Service for Gemini AI to enrich snake detection results with first aid advice
/// </summary>
public interface IGeminiAiService
{
    /// <summary>
    /// The AI model name configured (e.g. "gemini-2.0-flash").
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Get enriched snake information and first aid recommendations from Gemini
    /// </summary>
    Task<GeminiAnalysisResult> AnalyzeSnakeBiteAsync(GeminiAnalysisRequest request);

    /// <summary>
    /// RAG-based chat: send user message with system prompt + DB context → receive AI response.
    /// Supports multi-turn conversation via history parameter.
    /// </summary>
    Task<string> ChatWithContextAsync(
        string systemPrompt,
        string contextData,
        string userMessage,
        List<ChatHistoryItem>? history = null);
}

// ─── Existing records (snake analysis) ───

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

public record GeminiAnalysisResult(
    string DangerSummary,
    List<FirstAidStep> FirstAidSteps,
    string AiNote
);

public record FirstAidStep(
    int StepOrder,
    string Title,
    string Content
);

// ─── Chat records ───

public record ChatHistoryItem(
    string Role,   // "user" or "model"
    string Content
);