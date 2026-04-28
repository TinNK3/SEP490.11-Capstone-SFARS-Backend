using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<LocationTrackingHub> _logger;

    // Rate limit: connectionId → invalid attempt count (for public join)
    private static readonly ConcurrentDictionary<string, int> _publicAttempts = new();

    public LocationTrackingHub(IUnitOfWork unitOfWork, ILogger<LocationTrackingHub> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
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
        {
            _logger.LogWarning(
                "JoinIncidentTracking denied | ConnectionId={ConnectionId} | IncidentId={IncidentId} | Reason=UnauthorizedClaimMissing",
                Context.ConnectionId, incidentId);
            throw new HubException("Unauthorized");
        }

        var signalRGroup = LocationConstants.SignalRGroupPrefix + incidentId;
        _logger.LogInformation(
            "JoinIncidentTracking requested | ConnectionId={ConnectionId} | UserId={UserId} | IncidentId={IncidentId} | Group={Group}",
            Context.ConnectionId, userId, incidentId, signalRGroup);

        // Single query: is user a participant in an active incident?
        var isParticipant = await _unitOfWork.Repository<Incident, Guid>()
            .AnyAsync(i => i.Id == incidentId
                && i.CurrentStatus != IncidentStatus.Closed
                && i.CurrentStatus != IncidentStatus.Cancelled
                && (i.VictimId == userId
                    || i.Missions.Any(m => m.RescuerId == userId
                        && (m.Status == RescueStatus.Accepted
                            || m.Status == RescueStatus.Arrived))));

        if (!isParticipant)
        {
            _logger.LogWarning(
                "JoinIncidentTracking denied | ConnectionId={ConnectionId} | UserId={UserId} | IncidentId={IncidentId} | Group={Group} | Reason=NotAuthorizedOrIncidentInactive",
                Context.ConnectionId, userId, incidentId, signalRGroup);
            throw new HubException("Not authorized or incident not active");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, signalRGroup);
        _logger.LogInformation(
            "JoinIncidentTracking success | ConnectionId={ConnectionId} | UserId={UserId} | IncidentId={IncidentId} | Group={Group}",
            Context.ConnectionId, userId, incidentId, signalRGroup);
    }

    /// <summary>
    /// Public join via QR tracking code (no auth required).
    /// </summary>
    public async Task JoinIncidentTrackingPublic(string trackingCode)
    {
        var connId = Context.ConnectionId;

        var attempts = _publicAttempts.GetOrAdd(connId, 0);
        _logger.LogInformation(
            "JoinIncidentTrackingPublic requested | ConnectionId={ConnectionId} | TrackingCode={TrackingCode} | AttemptCount={AttemptCount}",
            connId, trackingCode, attempts);
        if (attempts >= 5)
        {
            _logger.LogWarning(
                "JoinIncidentTrackingPublic denied | ConnectionId={ConnectionId} | TrackingCode={TrackingCode} | Reason=TooManyInvalidAttempts",
                connId, trackingCode);
            throw new HubException("Too many invalid attempts");
        }

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
            _logger.LogWarning(
                "JoinIncidentTrackingPublic denied | ConnectionId={ConnectionId} | TrackingCode={TrackingCode} | AttemptCount={AttemptCount} | Reason=InvalidOrExpiredTrackingCode",
                connId, trackingCode, _publicAttempts[connId]);
            throw new HubException("Invalid or expired tracking code");
        }

        var signalRGroup = LocationConstants.SignalRGroupPrefix + incident.Id;
        _publicAttempts.TryRemove(connId, out _);
        await Groups.AddToGroupAsync(connId, signalRGroup);
        _logger.LogInformation(
            "JoinIncidentTrackingPublic success | ConnectionId={ConnectionId} | IncidentId={IncidentId} | Group={Group}",
            connId, incident.Id, signalRGroup);
    }

    public async Task LeaveIncidentTracking(Guid incidentId)
    {
        var signalRGroup = LocationConstants.SignalRGroupPrefix + incidentId;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, signalRGroup);
        _logger.LogInformation(
            "LeaveIncidentTracking success | ConnectionId={ConnectionId} | IncidentId={IncidentId} | Group={Group}",
            Context.ConnectionId, incidentId, signalRGroup);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _publicAttempts.TryRemove(Context.ConnectionId, out _);
        _logger.LogInformation(
            "LocationTrackingHub disconnected | ConnectionId={ConnectionId} | Exception={Exception}",
            Context.ConnectionId, exception?.Message);
        return base.OnDisconnectedAsync(exception);
    }

    #region Video Call Signaling

    /// <summary>
    /// Notify other participants in the incident group that a call has been accepted.
    /// </summary>
    [Authorize]
    public async Task AcceptVideoCall(Guid incidentId)
    {
        var signalRGroup = LocationConstants.SignalRGroupPrefix + incidentId;
        var payload = new
        {
            IncidentId = incidentId
        };

        _logger.LogInformation(
            "Sending SignalR video call accepted | ConnectionId={ConnectionId} | IncidentId={IncidentId} | Group={Group} | Event={Event} | Payload={Payload}",
            Context.ConnectionId, incidentId, signalRGroup, VideoCallConstants.EventCallAccepted, payload);

        await Clients.OthersInGroup(signalRGroup)
            .SendAsync(VideoCallConstants.EventCallAccepted, new
            {
                IncidentId = incidentId
            });

        _logger.LogInformation(
            "SignalR video call accepted sent | ConnectionId={ConnectionId} | IncidentId={IncidentId} | Group={Group} | Event={Event}",
            Context.ConnectionId, incidentId, signalRGroup, VideoCallConstants.EventCallAccepted);
    }

    /// <summary>
    /// Notify other participants that the call was rejected.
    /// </summary>
    [Authorize]
    public async Task RejectVideoCall(Guid incidentId, string reason)
    {
        var signalRGroup = LocationConstants.SignalRGroupPrefix + incidentId;
        var payload = new
        {
            IncidentId = incidentId,
            Reason = reason
        };

        _logger.LogInformation(
            "Sending SignalR video call rejected | ConnectionId={ConnectionId} | IncidentId={IncidentId} | Group={Group} | Event={Event} | Payload={Payload}",
            Context.ConnectionId, incidentId, signalRGroup, VideoCallConstants.EventCallRejected, payload);

        await Clients.OthersInGroup(signalRGroup)
            .SendAsync(VideoCallConstants.EventCallRejected, new
            {
                IncidentId = incidentId,
                Reason = reason
            });

        _logger.LogInformation(
            "SignalR video call rejected sent | ConnectionId={ConnectionId} | IncidentId={IncidentId} | Group={Group} | Event={Event}",
            Context.ConnectionId, incidentId, signalRGroup, VideoCallConstants.EventCallRejected);
    }

    /// <summary>
    /// Notify other participants that the call has ended.
    /// </summary>
    [Authorize]
    public async Task EndVideoCall(Guid incidentId)
    {
        var signalRGroup = LocationConstants.SignalRGroupPrefix + incidentId;
        var payload = new
        {
            IncidentId = incidentId
        };

        _logger.LogInformation(
            "Sending SignalR video call ended | ConnectionId={ConnectionId} | IncidentId={IncidentId} | Group={Group} | Event={Event} | Payload={Payload}",
            Context.ConnectionId, incidentId, signalRGroup, VideoCallConstants.EventCallEnded, payload);

        await Clients.OthersInGroup(signalRGroup)
            .SendAsync(VideoCallConstants.EventCallEnded, new
            {
                IncidentId = incidentId
            });

        _logger.LogInformation(
            "SignalR video call ended sent | ConnectionId={ConnectionId} | IncidentId={IncidentId} | Group={Group} | Event={Event}",
            Context.ConnectionId, incidentId, signalRGroup, VideoCallConstants.EventCallEnded);
    }

    #endregion
}
