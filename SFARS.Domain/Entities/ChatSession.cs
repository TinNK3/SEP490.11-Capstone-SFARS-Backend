using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

/// <summary>
/// General chat session — not tied to any incident.
/// Users can ask about snakes, first-aid, symptoms, etc.
/// </summary>
public class ChatSession : BaseEntity
{
    public Guid UserId { get; set; }
    public string? Title { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual User User { get; set; } = null!;
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}