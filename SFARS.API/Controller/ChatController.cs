using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Chat;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Params;

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
    public async Task<IActionResult> GetChatSessionsAsync([FromQuery] BaseSpecParams specParams)
    {
        var userId = User.GetUserId();
        var result = await _chatService.GetSessionsAsync(userId, specParams);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// Get messages of a chat session (paginated)
    /// </summary>
    [Authorize]
    [HttpGet(APIRoute.Chat.GetMessages, Name = nameof(GetChatMessagesAsync))]
    public async Task<IActionResult> GetChatMessagesAsync(
        [FromRoute] Guid id,
        [FromQuery] BaseSpecParams specParams)
    {
        var userId = User.GetUserId();
        var result = await _chatService.GetMessagesAsync(userId, id, specParams);
        return this.ToIActionResult(result);
    }

    /// <summary>
    /// Delete a chat session
    /// </summary>
    [Authorize]
    [HttpDelete(APIRoute.Chat.DeleteSession, Name = nameof(DeleteChatSessionAsync))]
    public async Task<IActionResult> DeleteChatSessionAsync([FromRoute] Guid id)
    {
        var userId = User.GetUserId();
        var result = await _chatService.DeleteSessionAsync(userId, id);
        return this.ToIActionResult(result);
    }
}