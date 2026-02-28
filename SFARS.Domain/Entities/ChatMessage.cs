using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

/// <summary>
/// Message in a general chat session.
/// Stores both user questions and AI responses with context traceability.
/// </summary>
public class ChatMessage : BaseEntity
{
    public Guid ChatSessionId { get; set; }
    public ChatSenderType SenderType { get; set; }
    public string Content { get; set; } = null!;

    /// <summary>
    /// JSON snapshot of the RAG context used to generate this AI response.
    /// Null for user messages. Enables traceability and debugging.
    /// </summary>
    public string? ContextSnapshotJson { get; set; }

    /// <summary>
    /// AI model name used (e.g. "gemini-2.0-flash"). Null for user messages.
    /// </summary>
    public string? ModelName { get; set; }

    // Navigation
    public virtual ChatSession ChatSession { get; set; } = null!;
}