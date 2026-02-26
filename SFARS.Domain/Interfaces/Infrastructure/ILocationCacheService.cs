using SFARS.Domain.Models;

namespace SFARS.Domain.Interfaces.Infrastructure;

/// <summary>
/// Abstraction for location caching operations.
/// Production implementation uses Redis; can be swapped for in-memory in tests.
/// </summary>
public interface ILocationCacheService
{
    /// <summary>
    /// Cache user's latest location (write-through after DB save).
    /// </summary>
    Task SetUserLocationAsync(Guid userId, double lat, double lng,
        DateTime updatedAt, double? accuracy);

    /// <summary>
    /// Get cached user location. Returns null on cache miss.
    /// </summary>
    Task<CachedLocation?> GetUserLocationAsync(Guid userId);

    /// <summary>
    /// Atomic distributed throttle check.
    /// Returns true if broadcast is allowed (no recent broadcast within interval).
    /// Uses Redis SETNX for multi-instance safety.
    /// </summary>
    Task<bool> TryAcquireBroadcastThrottleAsync(Guid userId, TimeSpan interval);

    /// <summary>
    /// Cache the list of active incident IDs for a user (as victim or rescuer).
    /// Short TTL (30s) — auto-invalidated on status change.
    /// </summary>
    Task CacheUserActiveIncidentsAsync(Guid userId, IEnumerable<Guid> incidentIds, TimeSpan ttl);

    /// <summary>
    /// Get cached active incident IDs. Returns null on cache miss.
    /// </summary>
    Task<IReadOnlyList<Guid>?> GetUserActiveIncidentsAsync(Guid userId);

    /// <summary>
    /// Invalidate cached active incidents (called on status change).
    /// </summary>
    Task InvalidateUserActiveIncidentsAsync(Guid userId);
}