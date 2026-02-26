using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Extensions;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Models;
using SFARS.Domain.Specifications;
using SFARS.Infrastructure.Helpers;
using SFARS.Infrastructure.Hubs;

namespace SFARS.Infrastructure.Services;

/// <summary>
/// Production-grade location broadcaster with Redis caching:
/// 1. Distributed throttle via Redis SETNX (multi-instance safe)
/// 2. User location read from Redis cache (0 DB queries on cache hit)
/// 3. Active incidents cached in Redis (30s TTL, 0 DB queries on cache hit)
/// 
/// Worst case (all cache miss): 3 DB queries → cached for next calls.
/// Best case (all cache hit): 0 DB queries.
/// </summary>
public class LocationBroadcastService : ILocationBroadcastService
{
    private readonly IHubContext<LocationTrackingHub> _hubContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocationCacheService _cacheService;
    private readonly ILogger<LocationBroadcastService> _logger;

    private static readonly TimeSpan ThrottleInterval = LocationConstants.BroadcastThrottleInterval;
    private static readonly TimeSpan IncidentsCacheTtl = LocationConstants.UserIncidentsCacheTtl;

    public LocationBroadcastService(
        IHubContext<LocationTrackingHub> hubContext,
        IUnitOfWork unitOfWork,
        ILocationCacheService cacheService,
        ILogger<LocationBroadcastService> logger)
    {
        _hubContext = hubContext;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task BroadcastLocationToIncidentsAsync(Guid userId)
    {
        // Step 1: Distributed throttle via Redis SETNX (multi-instance safe)
        if (!await _cacheService.TryAcquireBroadcastThrottleAsync(userId, ThrottleInterval))
            return;

        // Step 2: Read user location from Redis cache (written by LocationUpdatedEventHandler)
        var location = await _cacheService.GetUserLocationAsync(userId);
        if (location == null)
        {
            // Cache miss: fall back to DB (first call or Redis restart)
            location = await FallbackLoadUserLocationAsync(userId);
            if (location == null) return;
        }

        // Step 3: Get active incident IDs (cache-first with 30s TTL)
        var incidentIds = await _cacheService.GetUserActiveIncidentsAsync(userId);
        if (incidentIds == null)
        {
            incidentIds = await QueryActiveIncidentIdsFromDbAsync(userId);
            await _cacheService.CacheUserActiveIncidentsAsync(userId, incidentIds, IncidentsCacheTtl);
        }

        if (incidentIds.Count == 0) return;

        // Step 4: Broadcast minimal payload to each incident group
        var payload = new
        {
            userId = location.UserId,
            latitude = location.Latitude,
            longitude = location.Longitude,
            updatedAt = location.UpdatedAt,
            accuracy = location.AccuracyMeters,
            accuracyLevel = location.AccuracyLevel
        };

        foreach (var incidentId in incidentIds)
        {
            await _hubContext.Clients
                .Group(LocationConstants.SignalRGroupPrefix + incidentId)
                .SendAsync(LocationConstants.SignalRReceiveLocationUpdate, payload);
        }
    }

    /// <summary>
    /// Fallback: load user location from DB when Redis cache misses.
    /// </summary>
    private async Task<CachedLocation?> FallbackLoadUserLocationAsync(Guid userId)
    {
        var user = await _unitOfWork.Repository<User, Guid>().GetByIdAsync(userId);
        if (user?.CurrentLocation == null) return null;

        var cached = new CachedLocation(
            user.Id,
            user.CurrentLocation.Y,
            user.CurrentLocation.X,
            user.LocationUpdatedAt ?? DateTime.UtcNow,
            user.LocationAccuracyMeters,
            LocationHelper.GetAccuracyLevel(user.LocationAccuracyMeters));

        // Write to cache for next time
        await _cacheService.SetUserLocationAsync(
            user.Id, cached.Latitude, cached.Longitude, cached.UpdatedAt, cached.AccuracyMeters);

        return cached;
    }

    /// <summary>
    /// Query active incident IDs from DB (both as victim and rescuer).
    /// Result is cached for 30s by caller.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> QueryActiveIncidentIdsFromDbAsync(Guid userId)
    {
        var asVictim = await _unitOfWork.Repository<Incident, Guid>()
            .GetAllWithSpecAndSelectorAsync(
                new BaseSpecification<Incident>(i =>
                    i.VictimId == userId
                    && i.CurrentStatus != IncidentStatus.Closed
                    && i.CurrentStatus != IncidentStatus.Cancelled),
                i => i.Id,
                tracked: false);

        var asRescuer = await _unitOfWork.Repository<RescueMission, Guid>()
            .GetAllWithSpecAndSelectorAsync(
                new BaseSpecification<RescueMission>(m =>
                    m.RescuerId == userId
                    && (m.Status == RescueStatus.Accepted || m.Status == RescueStatus.Pending)
                    && m.Incident.CurrentStatus != IncidentStatus.Closed
                    && m.Incident.CurrentStatus != IncidentStatus.Cancelled),
                m => m.IncidentId,
                tracked: false);

        return asVictim.Concat(asRescuer).Distinct().ToList();
    }
}