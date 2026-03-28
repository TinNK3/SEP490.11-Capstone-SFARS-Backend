using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SFARS.Infrastructure.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    // Clients are automatically grouped by their UserId (ClaimTypes.NameIdentifier) thanks to [Authorize].
    // The server will push messages to specific users using:
    // _hubContext.Clients.User(userId.ToString()).SendAsync(...)
}
