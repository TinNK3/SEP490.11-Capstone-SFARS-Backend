using MediatR;
using Microsoft.Extensions.Logging;
using SFARS.Domain.Interfaces.Infrastructure;

namespace SFARS.Application.Events.Handlers;

/// <summary>
/// Handles LocationUpdatedEvent:
/// 1. Writes location to Redis cache (sub-ms)
/// 2. Triggers SignalR broadcast via ILocationBroadcastService
/// 
/// Runs asynchronously after the HTTP response is sent.
/// Exceptions are logged and swallowed — broadcast failure must never block the user.
/// </summary>
public class LocationUpdatedEventHandler : INotificationHandler<LocationUpdatedEvent>
{
    private readonly ILocationCacheService _cacheService;
    private readonly ILocationBroadcastService _broadcastService;
    private readonly ILogger<LocationUpdatedEventHandler> _logger;

    public LocationUpdatedEventHandler(
        ILocationCacheService cacheService,
        ILocationBroadcastService broadcastService,
        ILogger<LocationUpdatedEventHandler> logger)
    {
        _cacheService = cacheService;
        _broadcastService = broadcastService;
        _logger = logger;
    }

    public async Task Handle(LocationUpdatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Write to Redis cache (write-through)
            await _cacheService.SetUserLocationAsync(
                notification.UserId,
                notification.Latitude,
                notification.Longitude,
                notification.UpdatedAt,
                notification.AccuracyMeters);

            // Step 2: Broadcast to active incident groups
            await _broadcastService.BroadcastLocationToIncidentsAsync(notification.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to process location event for user {UserId}. " +
                "Location is persisted in DB but cache/broadcast may be stale.",
                notification.UserId);
            // Swallow: DB save already succeeded, cache/broadcast is best-effort
        }
    }
}