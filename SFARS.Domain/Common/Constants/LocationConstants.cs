namespace SFARS.Domain.Common.Constants;

/// <summary>
/// Centralized constants for location tracking, Redis cache keys,
/// SignalR events, and accuracy classification thresholds.
///
/// Naming convention for Redis keys: "sfars:{domain}:{entity}:{id}"
/// Example: "sfars:user:loc:{userId}"
/// </summary>
public static class LocationConstants
{
    // Redis Key Prefixes
    /// <summary>Redis HASH key prefix for user location — "sfars:user:loc:{userId}"</summary>
    public const string RedisLocKeyPrefix = "sfars:user:loc:";

    /// <summary>Redis STRING key prefix for broadcast throttle — "sfars:broadcast:throttle:{userId}"</summary>
    public const string RedisBroadcastThrottleKeyPrefix = "sfars:broadcast:throttle:";

    /// <summary>Redis STRING key prefix for user active incident IDs — "sfars:user:incidents:{userId}"</summary>
    public const string RedisUserIncidentsKeyPrefix = "sfars:user:incidents:";

    /// <summary>IMemoryCache key for all active medical facilities</summary>
    public const string MemCacheFacilitiesActiveKey = "sfars:facilities:active";

    // Redis Hash Field Names (for location HSET)
    public const string FieldUserId = "userId";
    public const string FieldLat = "lat";
    public const string FieldLng = "lng";
    public const string FieldUpdatedAt = "updatedAt";
    public const string FieldAccuracy = "accuracy";
    public const string FieldAccuracyLevel = "accuracyLevel";

    // Accuracy Level Strings
    public const string AccuracyHigh = "high";
    public const string AccuracyMedium = "medium";
    public const string AccuracyLow = "low";
    public const string AccuracyUnknown = "unknown";

    // Accuracy Thresholds (meters)
    /// <summary>Accuracy better than this is classified as "high"</summary>
    public const double AccuracyHighThresholdMeters = 30;

    /// <summary>Accuracy between HighThreshold and this is classified as "medium"</summary>
    public const double AccuracyMediumThresholdMeters = 100;

    /// <summary>Discard tracking log if accuracy is worse than this</summary>
    public const double AccuracyDiscardThresholdMeters = 200;

    // Tracking Log Sampling Rules
    /// <summary>Minimum movement in meters before writing a new tracking log entry</summary>
    public const double TrackingLogMinMovementMeters = 50;

    /// <summary>Minimum time in seconds before writing a new tracking log entry regardless of movement</summary>
    public const double TrackingLogMinIntervalSeconds = 30;

    // TTL Configuration
    /// <summary>How long a user location is cached in Redis before it's considered stale</summary>
    public static readonly TimeSpan LocationCacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>Minimum interval between two broadcasts for the same user (distributed throttle)</summary>
    public static readonly TimeSpan BroadcastThrottleInterval = TimeSpan.FromSeconds(2);

    /// <summary>How long the active incident IDs list is cached per user</summary>
    public static readonly TimeSpan UserIncidentsCacheTtl = TimeSpan.FromSeconds(30);

    /// <summary>How long the full facility list is cached in-memory</summary>
    public static readonly TimeSpan FacilitiesCacheTtl = TimeSpan.FromMinutes(5);

    // Tracking Code
    /// <summary>How long a tracking code is valid after generation</summary>
    public static readonly TimeSpan TrackingCodeExpiry = TimeSpan.FromHours(24);

    // SignalR
    /// <summary>SignalR group name prefix for incident tracking rooms — "incident-{incidentId}"</summary>
    public const string SignalRGroupPrefix = "incident-";

    /// <summary>SignalR event name pushed from server to clients when a location is updated</summary>
    public const string SignalRReceiveLocationUpdate = "ReceiveLocationUpdate";

    /// <summary>SignalR Redis backplane channel prefix</summary>
    public const string SignalRRedisChannelPrefix = "SFARS:SignalR:";
}