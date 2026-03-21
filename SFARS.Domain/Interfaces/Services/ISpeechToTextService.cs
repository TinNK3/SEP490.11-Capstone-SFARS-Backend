namespace SFARS.Domain.Interfaces.Services;

/// <summary>
/// Result from processing an audio voice note using Multi-modal AI.
/// Contains the raw text as well as structured extracted data.
/// </summary>
public record AudioExtractionResult(
    string? Transcript,
    int? MinutesSinceBite,
    List<string>? Symptoms
);

/// <summary>
/// Interface for Voice Symptom Extraction services.
/// Used to transcribe and intelligently extract parameters from audio.
/// </summary>
public interface ISpeechToTextService
{
    /// <summary>
    /// Transcribes the audio and extracts symptomatic context.
    /// </summary>
    Task<AudioExtractionResult?> TranscribeAndExtractAsync(Stream audioStream, string fileName, string contentType);
}