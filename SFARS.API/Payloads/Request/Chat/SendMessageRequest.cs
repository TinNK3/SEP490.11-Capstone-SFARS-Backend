using System.ComponentModel.DataAnnotations;

namespace SFARS.API.Payloads.Request.Chat;

/// <summary>
/// Request payload for sending a message in a chat session.
/// If SessionId is null, a new session is created automatically.
/// </summary>
public class SendMessageRequest
{
    /// <summary>
    /// Existing session ID. If null, a new session will be created.
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// User's message content
    /// </summary>
    [Required(ErrorMessage = "Message content is required")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 2000 characters")]
    public string Content { get; set; } = null!;
}