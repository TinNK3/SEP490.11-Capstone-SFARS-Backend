using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

/// <summary>
/// Represents a Gemini AI API key managed at runtime by admins.
/// Keys are rotated automatically when quota is exhausted.
/// </summary>
public class GeminiApiKey : BaseEntity
{
    /// <summary>Encrypted or plain API key value.</summary>
    public string KeyValue { get; set; } = null!;

    /// <summary>Human-readable label for identification, e.g. "Key A - Gmail Project 1".</summary>
    public string? Label { get; set; }

    /// <summary>Admin toggle — disabled keys are skipped during rotation.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Auto-set when the key returns 429/403. Reset daily by scheduled job.</summary>
    public bool IsExhausted { get; set; }

    /// <summary>Timestamp when the key was marked as exhausted.</summary>
    public DateTime? ExhaustedAt { get; set; }

    /// <summary>Last time this key was used in a successful API call.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>Cumulative count of successful API calls made with this key.</summary>
    public int TotalUsageCount { get; set; }

    /// <summary>Consecutive failure counter — reset on success.</summary>
    public int ConsecutiveFailures { get; set; }
}