namespace SFARS.Domain.Interfaces.Infrastructure;

/// <summary>
/// Service for Gemini AI — snake detection (vision), first-aid enrichment, and RAG chat.
/// </summary>
public interface IGeminiAiService
{
    /// <summary>
    /// The AI model name configured (e.g. "gemini-2.0-flash").
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Analyze an image using Gemini Vision to determine whether it contains a snake.
    /// Replaces the binary ONNX classification model for higher accuracy.
    /// </summary>
    /// <param name="imageBytes">Raw image bytes (JPEG/PNG).</param>
    /// <param name="mimeType">MIME type of the image (e.g. "image/jpeg").</param>
    /// <returns>Detection result with confidence and optional reasoning.</returns>
    Task<GeminiSnakeDetectionResult> DetectSnakeInImageAsync(byte[] imageBytes, string mimeType);

    /// <summary>
    /// Get enriched snake information and first aid recommendations from Gemini.
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

// ─── Snake detection (Gemini Vision) ───

/// <summary>
/// Result from Gemini Vision snake detection — replaces binary ONNX model output.
/// </summary>
public record GeminiSnakeDetectionResult(
    bool IsSnake,
    float Confidence,
    string? Reasoning
);

// ─── Snake analysis (first-aid enrichment) ───

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