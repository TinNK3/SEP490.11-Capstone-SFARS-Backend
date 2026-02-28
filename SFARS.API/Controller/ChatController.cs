using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Chat;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.API.Controller;

/// <summary>
/// General AI chatbox endpoints (RAG-based, not tied to incidents)
/// </summary>
[ApiController]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>
    /// Send a message and receive AI response.
    /// Auto-creates a new session if SessionId is not provided.
    /// </summary>
    [Authorize]
    [HttpPost(APIRoute.Chat.SendMessage, Name = nameof(SendMessageAsync))]
    public async Task<IActionResult> SendMessageAsync([FromBody] SendMessageRequest req)
    {
        var userId = User.GetUserId();
        var result = await _chatService.SendMessageAsync(userId, req.SessionId, req.Content);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// Get user's chat sessions (paginated)
    /// </summary>
    [Authorize]
    [HttpGet(APIRoute.Chat.GetSessions, Name = nameof(GetChatSessionsAsync))]
    public async Task<IActionResult> GetChatSessionsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = User.GetUserId();
        var result = await _chatService.GetSessionsAsync(userId, page, pageSize);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// Get messages of a chat session (paginated)
    /// </summary>
    [Authorize]
    [HttpGet(APIRoute.Chat.GetMessages, Name = nameof(GetChatMessagesAsync))]
    public async Task<IActionResult> GetChatMessagesAsync(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var userId = User.GetUserId();
        var result = await _chatService.GetMessagesAsync(userId, id, page, pageSize);
        return this.ToIActionResult(result);
    }
}