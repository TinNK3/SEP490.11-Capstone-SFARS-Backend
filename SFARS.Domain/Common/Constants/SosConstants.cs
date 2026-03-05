namespace SFARS.Domain.Common.Constants;

/// <summary>
/// Centralized constants for the SOS grace-period countdown and anti-spam guard.
///
/// Redis key naming convention (same as LocationConstants): "sfars:{domain}:{entity}:{id}"
/// </summary>
public static class SosConstants
{
    // ── Grace Period ──────────────────────────────────────────────────────────
    /// <summary>
    /// How long the client countdown lasts before dispatch begins.
    /// Backend uses this as the "soft cancel" window.
    /// </summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(10);

    // ── Anti-Spam Redis Keys ──────────────────────────────────────────────────
    /// <summary>
    /// Redis HASH key: tracks how many times a user cancelled an SOS within the window.
    /// Key format: "sfars:sos:spam:{userId}"
    /// </summary>
    public const string RedisSpamKeyPrefix = "sfars:sos:spam:";

    /// <summary>
    /// Redis STRING key: set when a user is hard-blocked from creating SOSs.
    /// Key format: "sfars:sos:block:{userId}"
    /// </summary>
    public const string RedisBlockKeyPrefix = "sfars:sos:block:";

    // ── Thresholds ────────────────────────────────────────────────────────────
    /// <summary>
    /// Number of SOS cancellations in <see cref="SpamWindowTtl"/> before a soft warning is shown.
    /// </summary>
    public const int SoftCancelThreshold = 2;

    /// <summary>
    /// Number of SOS cancellations in <see cref="SpamWindowTtl"/> before the user is hard-blocked.
    /// </summary>
    public const int HardCancelThreshold = 3;

    /// <summary>Sliding window over which cancellation count is measured.</summary>
    public static readonly TimeSpan SpamWindowTtl = TimeSpan.FromMinutes(10);

    /// <summary>How long a hard-blocked user must wait before creating another SOS.</summary>
    public static readonly TimeSpan BlockDuration = TimeSpan.FromMinutes(30);
}