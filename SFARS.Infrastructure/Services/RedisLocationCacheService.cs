using Microsoft.Extensions.Logging;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Models;
using SFARS.Infrastructure.Helpers;
using StackExchange.Redis;
using System.Text.Json;

namespace SFARS.Infrastructure.Services;

/// <summary>
/// Redis-backed location cache service.
/// Uses StackExchange.Redis directly (not IDistributedCache) for:
///   - HSET/HGETALL for structured location data
///   - SET NX EX for atomic distributed throttle
///   - Proper key expiration management
/// All keys and field names are defined in LocationConstants to avoid magic strings.
/// </summary>
public class RedisLocationCacheService : ILocationCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisLocationCacheService> _logger;

    public RedisLocationCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisLocationCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SetUserLocationAsync(
        Guid userId, double lat, double lng, DateTime updatedAt, double? accuracy)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = LocationConstants.RedisLocKeyPrefix + userId;

            var entries = new HashEntry[]
            {
                new(LocationConstants.FieldUserId,        userId.ToString()),
                new(LocationConstants.FieldLat,           lat.ToString("R")),
                new(LocationConstants.FieldLng,           lng.ToString("R")),
                new(LocationConstants.FieldUpdatedAt,     updatedAt.ToString("O")),
                new(LocationConstants.FieldAccuracy,      accuracy?.ToString("R") ?? ""),
                new(LocationConstants.FieldAccuracyLevel, LocationHelper.GetAccuracyLevel(accuracy))
            };

            await db.HashSetAsync(key, entries);
            await db.KeyExpireAsync(key, LocationConstants.LocationCacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache location for user {UserId}", userId);
        }
    }

    /// <inheritdoc/>
    public async Task<CachedLocation?> GetUserLocationAsync(Guid userId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var entries = await db.HashGetAllAsync(LocationConstants.RedisLocKeyPrefix + userId);

            if (entries.Length == 0) return null;

            var dict = entries.ToDictionary(
                e => e.Name.ToString(),
                e => e.Value.ToString());

            return new CachedLocation(
                UserId: Guid.Parse(dict[LocationConstants.FieldUserId]),
                Latitude: double.Parse(dict[LocationConstants.FieldLat]),
                Longitude: double.Parse(dict[LocationConstants.FieldLng]),
                UpdatedAt: DateTime.Parse(dict[LocationConstants.FieldUpdatedAt]),
                AccuracyMeters: string.IsNullOrEmpty(dict[LocationConstants.FieldAccuracy])
                    ? null
                    : double.Parse(dict[LocationConstants.FieldAccuracy]),
                AccuracyLevel: dict[LocationConstants.FieldAccuracyLevel]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read cached location for user {UserId}", userId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TryAcquireBroadcastThrottleAsync(Guid userId, TimeSpan interval)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = LocationConstants.RedisBroadcastThrottleKeyPrefix + userId;

            // SET key "1" NX EX <seconds> — atomic: returns true only if key didn't exist
            return await db.StringSetAsync(key, "1", interval, When.NotExists);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed throttle check for user {UserId}, allowing broadcast", userId);
            return true; // On Redis failure, allow broadcast (fail-open)
        }
    }

    /// <inheritdoc/>
    public async Task CacheUserActiveIncidentsAsync(
        Guid userId, IEnumerable<Guid> incidentIds, TimeSpan ttl)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = LocationConstants.RedisUserIncidentsKeyPrefix + userId;
            var json = JsonSerializer.Serialize(incidentIds.ToList());
            await db.StringSetAsync(key, json, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache active incidents for user {UserId}", userId);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Guid>?> GetUserActiveIncidentsAsync(Guid userId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = await db.StringGetAsync(LocationConstants.RedisUserIncidentsKeyPrefix + userId);

            if (json.IsNullOrEmpty) return null;

            return JsonSerializer.Deserialize<List<Guid>>(json!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read cached incidents for user {UserId}", userId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task InvalidateUserActiveIncidentsAsync(Guid userId)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(LocationConstants.RedisUserIncidentsKeyPrefix + userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cached incidents for user {UserId}", userId);
        }
    }
}