namespace SFARS.Domain.Models;

/// <summary>
/// Lightweight cache-only record for user location.
/// Not an EF entity — used for Redis serialization without EF overhead.
/// </summary>
public record CachedLocation(
    Guid UserId,
    double Latitude,
    double Longitude,
    DateTime UpdatedAt,
    double? AccuracyMeters,
    string AccuracyLevel
);