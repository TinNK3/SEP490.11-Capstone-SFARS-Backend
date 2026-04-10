using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Specifications;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace SFARS.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub for real-time location tracking and incident communication.
/// Clients join group "incident-{incidentId}" to receive updates.
/// </summary>
public class LocationTrackingHub : Hub
{
    private readonly IUnitOfWork _unitOfWork;

    // Rate limit: connectionId → invalid attempt count (for public join)
    private static readonly ConcurrentDictionary<string, int> _publicAttempts = new();

    public LocationTrackingHub(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Authenticated participants join an incident tracking room.
    /// Verifies user is victim or active rescuer in the incident.
    /// </summary>
    [Authorize]
    public async Task JoinIncidentTracking(Guid incidentId)
    {
        var userIdClaim = Context.User?
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
            throw new HubException("Unauthorized");

        // Single query: is user a participant in an active incident?
        var isParticipant = await _unitOfWork.Repository<Incident, Guid>()
            .AnyAsync(i => i.Id == incidentId
                && i.CurrentStatus != IncidentStatus.Closed
                && i.CurrentStatus != IncidentStatus.Cancelled
                && (i.VictimId == userId
                    || i.Missions.Any(m => m.RescuerId == userId
                        && (m.Status == RescueStatus.Accepted
                            || m.Status == RescueStatus.Pending))));

        if (!isParticipant)
            throw new HubException("Not authorized or incident not active");

        await Groups.AddToGroupAsync(Context.ConnectionId, LocationConstants.SignalRGroupPrefix + incidentId);
    }

    /// <summary>
    /// Public join via QR tracking code (no auth required).
    /// </summary>
    public async Task JoinIncidentTrackingPublic(string trackingCode)
    {
        var connId = Context.ConnectionId;

        var attempts = _publicAttempts.GetOrAdd(connId, 0);
        if (attempts >= 5)
            throw new HubException("Too many invalid attempts");

        var incident = await _unitOfWork.Repository<Incident, Guid>()
            .GetWithSpecAsync(new BaseSpecification<Incident>(i =>
                i.TrackingCode == trackingCode
                && i.TrackingCodeExpiresAt != null
                && i.TrackingCodeExpiresAt > DateTime.UtcNow
                && i.CurrentStatus != IncidentStatus.Closed
                && i.CurrentStatus != IncidentStatus.Cancelled));

        if (incident == null)
        {
            _publicAttempts[connId] = attempts + 1;
            throw new HubException("Invalid or expired tracking code");
        }

        _publicAttempts.TryRemove(connId, out _);
        await Groups.AddToGroupAsync(connId, LocationConstants.SignalRGroupPrefix + incident.Id);
    }

    public async Task LeaveIncidentTracking(Guid incidentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, LocationConstants.SignalRGroupPrefix + incidentId);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _publicAttempts.TryRemove(Context.ConnectionId, out _);
        return base.OnDisconnectedAsync(exception);
    }

    #region Video Call Signaling

    /// <summary>
    /// Notify other participants in the incident group that a call has been accepted.
    /// </summary>
    [Authorize]
    public async Task AcceptVideoCall(Guid incidentId)
    {
        await Clients.OthersInGroup(LocationConstants.SignalRGroupPrefix + incidentId)
            .SendAsync(VideoCallConstants.EventCallAccepted, incidentId);
    }

    /// <summary>
    /// Notify other participants that the call was rejected.
    /// </summary>
    [Authorize]
    public async Task RejectVideoCall(Guid incidentId, string reason)
    {
        await Clients.OthersInGroup(LocationConstants.SignalRGroupPrefix + incidentId)
            .SendAsync(VideoCallConstants.EventCallRejected, incidentId, reason);
    }

    /// <summary>
    /// Notify other participants that the call has ended.
    /// </summary>
    [Authorize]
    public async Task EndVideoCall(Guid incidentId)
    {
        await Clients.OthersInGroup(LocationConstants.SignalRGroupPrefix + incidentId)
            .SendAsync(VideoCallConstants.EventCallEnded, incidentId);
    }

    #endregion
}