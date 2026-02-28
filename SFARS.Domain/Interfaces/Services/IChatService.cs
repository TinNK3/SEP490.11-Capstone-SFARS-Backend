using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services;

/// <summary>
/// Service for general AI chatbox (RAG-based, not tied to incidents).
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Send a message and receive AI response.
    /// Auto-creates a new session if sessionId is null.
    /// </summary>
    Task<IServiceResult> SendMessageAsync(Guid userId, Guid? sessionId, string message);

    /// <summary>
    /// Get paginated list of user's chat sessions.
    /// </summary>
    Task<IServiceResult> GetSessionsAsync(Guid userId, int page, int pageSize);

    /// <summary>
    /// Get paginated messages of a specific session.
    /// </summary>
    Task<IServiceResult> GetMessagesAsync(Guid userId, Guid sessionId, int page, int pageSize);
}