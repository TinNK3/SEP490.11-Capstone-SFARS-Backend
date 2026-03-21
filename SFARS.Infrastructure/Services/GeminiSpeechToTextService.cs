using Microsoft.Extensions.Logging;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.Infrastructure.Services;

/// <summary>
/// A real implementation of ISpeechToTextService using Google Gemini's multi-modal capabilities.
/// </summary>
public class GeminiSpeechToTextService : ISpeechToTextService
{
    private readonly IGeminiAiService _geminiAiService;
    private readonly ILogger<GeminiSpeechToTextService> _logger;

    public GeminiSpeechToTextService(
        IGeminiAiService geminiAiService,
        ILogger<GeminiSpeechToTextService> logger)
    {
        _geminiAiService = geminiAiService;
        _logger = logger;
    }

    public async Task<AudioExtractionResult?> TranscribeAndExtractAsync(Stream audioStream, string fileName, string contentType)
    {
        try
        {
            _logger.LogInformation("Starting Real STT and Symptom Extraction for file {FileName} ({ContentType}) using Gemini", fileName, contentType);

            // Read stream into byte array
            if (audioStream.CanSeek)
            {
                audioStream.Position = 0;
            }

            using var memoryStream = new MemoryStream();
            await audioStream.CopyToAsync(memoryStream);
            var audioBytes = memoryStream.ToArray();

            // Gemini API supports specific mime types. If the app sends application/octet-stream, we might need a fallback.
            var mimeType = contentType;
            if (string.IsNullOrEmpty(mimeType) || !mimeType.StartsWith("audio/") && !mimeType.StartsWith("video/"))
            {
                // Default to a known audio container if browser couldn't determine
                mimeType = fileName.EndsWith(".webm") ? "video/webm" : 
                           fileName.EndsWith(".mp4") ? "video/mp4" : "audio/mpeg"; 
            }

            var extractionResult = await _geminiAiService.ExtractAudioSymptomsAsync(audioBytes, mimeType);
            
            _logger.LogInformation("Gemini Audio Extraction completed successfully.");
            
            return extractionResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract audio file {FileName} with Gemini", fileName);
            return null; // Return null so the main flow isn't interrupted, but we just lack the extraction
        }
    }
}