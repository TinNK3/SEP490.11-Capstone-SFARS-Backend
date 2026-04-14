using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;

namespace SFARS.Infrastructure.Hubs;

/// <summary>
/// Rescuer-facing SignalR hub for SOS dispatch notifications.
///
/// Responsibility: manage per-user dispatch group membership only.
/// IsOnline is NOT managed here — dispatch eligibility is determined
/// by User.LocationUpdatedAt heartbeat in DispatchService, which is
/// immune to stale data from app kills, network drops, or server restarts.
///
/// IsAvailable is also NOT managed here — it is a manual toggle
/// that rescuers control independently of their connection state.
///
/// Server pushes via IHubContext&lt;RescueDispatchHub&gt; — no client broadcast.
/// </summary>
[Authorize(Roles = "Rescuer")]
public class RescueDispatchHub : Hub
{
    private readonly IUnitOfWork _unitOfWork;

    public RescueDispatchHub(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Called by the rescuer app on startup / foreground.
    /// Joins the per-user dispatch group so DispatchService can push SOS cards.
    /// Requires verified rescuer profile.
    /// </summary>
    public async Task RegisterOnline()
    {
        var userId  = GetUserIdOrThrow();
        var profile = await _unitOfWork.Repository<RescuerProfile, Guid>().GetByIdAsync(userId);

        if (profile is not { IsVerified: true })
            throw new HubException("Rescuer not verified.");

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            DispatchConstants.RescuerGroupPrefix + userId);

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            DispatchConstants.AllRescuersGroup);
    }

    /// <summary>
    /// Called when the rescuer explicitly goes offline or closes the app gracefully.
    /// Leaves the dispatch group.
    /// </summary>
    public async Task RegisterOffline()
    {
        var userId = GetUserIdOrThrow();
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            DispatchConstants.RescuerGroupPrefix + userId);

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            DispatchConstants.AllRescuersGroup);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
        => base.OnDisconnectedAsync(exception);

    // Helpers

    private Guid GetUserIdOrThrow()
    {
        var claim = Context.User?
            .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(claim, out var id)) return id;
        throw new HubException("Unauthorized");
    }
}