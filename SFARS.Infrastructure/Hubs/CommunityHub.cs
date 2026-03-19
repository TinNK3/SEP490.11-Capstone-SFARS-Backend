using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SFARS.Infrastructure.Hubs;

[Authorize]
public class CommunityHub : Hub
{
    public const string FeedGroup = "community:feed";
    public static string PostGroup(Guid postId) => $"community:post-{postId}";

    public async Task JoinFeed()
        => await Groups.AddToGroupAsync(Context.ConnectionId, FeedGroup);

    public async Task LeaveFeed()
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, FeedGroup);

    public async Task JoinPost(Guid postId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, PostGroup(postId));

    public async Task LeavePost(Guid postId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, PostGroup(postId));

    public override Task OnDisconnectedAsync(Exception? exception)
        => base.OnDisconnectedAsync(exception);
}
