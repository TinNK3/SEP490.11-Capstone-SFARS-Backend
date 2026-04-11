using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Models;
using SFARS.Domain.Specifications;
using SFARS.Infrastructure.Helpers;
using SFARS.Infrastructure.Hubs;
using StackExchange.Redis;

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
    private readonly IHubContext<RescueDispatchHub> _rescueHub;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocationCacheService _cacheService;
    private readonly ILogger<LocationBroadcastService> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFcmPushService _fcmService;

    private static readonly TimeSpan ThrottleInterval = LocationConstants.BroadcastThrottleInterval;
    private static readonly TimeSpan IncidentsCacheTtl = LocationConstants.UserIncidentsCacheTtl;

    public LocationBroadcastService(
        IHubContext<LocationTrackingHub> hubContext,
        IHubContext<RescueDispatchHub> rescueHub,
        IUnitOfWork unitOfWork,
        ILocationCacheService cacheService,
        IConnectionMultiplexer redis,
        ILogger<LocationBroadcastService> logger,
        IServiceScopeFactory scopeFactory,
        IFcmPushService fcmService)
    {
        _hubContext = hubContext;
        _rescueHub = rescueHub;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _redis = redis;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _fcmService = fcmService;
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

        // --- GEOFENCING AUTO-SUGGEST ARRIVED ---
        // Fire-and-forget style to not block the broadcast of location
        _ = ProcessGeofenceArrivedSuggestionAsync(userId, location);
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

    /// <summary>
    /// Processes high-accuracy location pings to detect if a rescuer has arrived at the incident.
    /// Uses Redis to track consecutive hits within 50 meters to prevent GPS drift false-positives.
    /// </summary>
    private async Task ProcessGeofenceArrivedSuggestionAsync(Guid userId, CachedLocation location)
    {
        try
        {
            // Only process high-accuracy pings to avoid false positives bouncing around
            if (location.AccuracyMeters == null || location.AccuracyMeters > LocationConstants.GeofenceArrivedSuggestAccuracyMeters)
            {
                return;
            }

            var spec = new BaseSpecification<RescueMission>(m => 
                m.RescuerId == userId && 
                m.Status == RescueStatus.Accepted && 
                m.Incident.CurrentStatus == IncidentStatus.EnRoute);
            
            spec.ApplyInclude(q => q.Include(m => m.Incident));
            
            // Create a dedicated scope for this Fire-and-Forget background task!
            // This prevents "A second operation was started on this context" exception.
            using var scope = _scopeFactory.CreateScope();
            var bgUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var enRouteMissions = await bgUnitOfWork.Repository<RescueMission, Guid>().GetAllWithSpecAsync(spec);

            if (!enRouteMissions.Any()) return;

            var db = _redis.GetDatabase();
            var rescuerPoint = new Point(location.Longitude, location.Latitude) { SRID = 4326 };

            foreach (var mission in enRouteMissions)
            {
                var incident = mission.Incident;
                var distMeters = LocationHelper.HaversineMeters(rescuerPoint, incident.Location);

                string hitsKey = $"{LocationConstants.RedisGeofenceHitsPrefix}{incident.Id}:{userId}";
                string suggestedKey = $"{LocationConstants.RedisGeofenceSuggestedPrefix}{incident.Id}:{userId}";

                // 1. If we already suggested Arrived for this incident, stop tracking to save CPU
                if (await db.KeyExistsAsync(suggestedKey))
                    continue;

                // 2. Geofence Check
                if (distMeters <= LocationConstants.GeofenceArrivedSuggestDistanceMeters)
                {
                    var hits = await db.StringIncrementAsync(hitsKey);
                    await db.KeyExpireAsync(hitsKey, TimeSpan.FromMinutes(5));

                    // 3. Trigger Suggestion if threshold met
                    if (hits >= LocationConstants.GeofenceArrivedSuggestConsecutiveHits)
                    {
                        await db.StringSetAsync(suggestedKey, "1", TimeSpan.FromHours(4));
                        await db.KeyDeleteAsync(hitsKey);

                        var payload = new
                        {
                            IncidentId = incident.Id,
                            MissionId = mission.Id,
                            Message = LocationConstants.GeofenceArrivedSuggestMessage,
                            DistanceMeters = Math.Round(distMeters, 1)
                        };

                        await _rescueHub.Clients
                            .Group(DispatchConstants.RescuerGroupPrefix + userId)
                            .SendAsync(DispatchConstants.EventSuggestArrived, payload);

                        // FCM push (rescuer riding → app likely in background)
                        var fcmData = new Dictionary<string, string>
                        {
                            { "incidentId", incident.Id.ToString() },
                            { "missionId", mission.Id.ToString() },
                            { "type", DispatchConstants.FcmSuggestArrivedTitleKey }
                        };
                        await _fcmService.SendToUserAsync(
                            userId,
                            DispatchConstants.PushSuggestArrivedTitle,
                            DispatchConstants.PushSuggestArrivedBody,
                            fcmData);

                        _logger.LogInformation("GeoFence Triggered! Suggesting Arrived to Rescuer {RescuerId} for Incident {IncidentId}. Dist: {Dist}m", userId, incident.Id, distMeters);
                    }
                }
                else
                {
                    // Broke the consecutive chain
                    await db.KeyDeleteAsync(hitsKey);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing geofence arrived suggestion for user {UserId}", userId);
        }
    }
}