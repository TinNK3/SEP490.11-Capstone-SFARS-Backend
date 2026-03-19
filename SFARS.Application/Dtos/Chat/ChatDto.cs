using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Chat;

/// <summary>
/// Chat session summary for listing
/// </summary>
public class ChatSessionDto
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Single chat message
/// </summary>
public class ChatMessageDto
{
    public Guid Id { get; set; }
    public ChatSenderType SenderType { get; set; }
    public string Content { get; set; } = null!;
    public string? ModelName { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Response after sending a message (includes AI reply)
/// </summary>
public class SendMessageResponseDto
{
    public Guid SessionId { get; set; }
    public ChatMessageDto UserMessage { get; set; } = null!;
    public ChatMessageDto AiMessage { get; set; } = null!;
}