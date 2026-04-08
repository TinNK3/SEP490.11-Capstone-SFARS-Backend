namespace SFARS.Domain.Common.Constants;

/// <summary>
/// Centralized constants for SOS tiered dispatching and graceful fallback.
/// </summary>
public static class DispatchConstants
{
    // Tier Search Radii (meters — NTS STDistance unit)
    public const double Tier1RadiusMeters   = 5_000;
    public const double Tier2RadiusMeters   = 10_000;
    public const double Tier3RadiusMeters   = 20_000;

    /// <summary>
    /// Fail-fast radius: if no available rescuer exists within this distance,
    /// skip all tiers and trigger fallback immediately.
    /// </summary>
    public const double FailFastRadiusMeters = 20_000;

    // Tier ETA Thresholds (minutes)
    /// <summary>Average road speed used for ETA estimation.</summary>
    public const double AvgSpeedKmh = 30;
    public const int    Tier1EtaMinutes = 15;
    public const int    Tier2EtaMinutes = 30;
    public const int    Tier3EtaMinutes = 45;

    // Hangfire Job Delays
    public static readonly TimeSpan Tier1Delay    = TimeSpan.Zero;
    public static readonly TimeSpan Tier2Delay    = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan Tier3Delay    = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan FallbackDelay = TimeSpan.FromSeconds(90);

    // Hangfire Queue
    public const string HangfireQueue = "dispatch";

    /// <summary>
    /// A rescuer is considered "active" (eligible for dispatch) only if their
    /// location was updated within this window. Acts as a heartbeat / proof-of-life.
    /// More reliable than IsOnline which can go stale on app kills or network drops.
    /// </summary>
    public const int LocationHeartbeatWindowMinutes = 5;
    
    // Offline / FCM Freshness
    public const int Tier2FreshnessHours = 2; // FCM recent offline
    public const int Tier3FreshnessHours = 48; // FCM long offline

    // Limits
    public const int Tier1MaxRescuers = 20;
    public const int Tier2MaxRescuers = 50;
    public const int Tier3MaxRescuers = 100;
    
    // Claim Timeout & Watchdog
    public const int ClaimTimeoutMinutes = 10; // Limit incident block if rescuer no-shows
    
    // Watchdog Thresholds (Master Elite)
    public const int    WatchdogHeartbeatTimeoutMins = 10;
    public const double WatchdogMicroMovementThresholdMeters = 15.0; // GPS noise floor
    public const double WatchdogMacroProgressThresholdMeters = 500.0; // Significant distance change
    public const int    WatchdogInitialGraceMins = 15;
    public const int    WatchdogStandardIntervalMins = 10;

    // Abandoned Timeout
    public const int AbandonedIncidentExpiryHours = 2; // Auto-close Unassigned incidents

    // Firebase Messaging keys
    public const string FcmSosDispatchTitleKey = "sos_dispatch";

    // SignalR Group Prefix
    /// <summary>
    /// Per-user dispatch group: "dispatch:user:{userId}".
    /// DispatchService pushes to this group so only that rescuer receives the event.
    /// </summary>
    public const string RescuerGroupPrefix = "dispatch:user:";

    // SignalR Event Names
    /// <summary>Pushed to rescuers when a new SOS is dispatched in their tier.</summary>
    public const string EventNewDispatch   = "sos:dispatch";

    /// <summary>Pushed to the victim when all tiers fail — triggers fallback UI.</summary>
    public const string EventFallback      = "sos:fallback";

    /// <summary>Pushed to all rescuers in range when one has already accepted — dismiss card.</summary>
    public const string EventAssigned      = "sos:assigned";

    /// <summary>Pushed privately to rescuer when their Location pings trigger the Arrived Geofence.</summary>
    public const string EventSuggestArrived = "sos:suggest_arrived";

    // FCM Notification Text Constants
    public const string PushTitlePrefix = "SOS Rắn Cắn!";
    public const string PushUnknownSnake = "Chưa rõ loài";
    public const string PushBodyTemplate = "{0}. Cách bạn {1}km. Chạm để xem và nhận ca!";

    // Symptom Update — SignalR + FCM
    /// <summary>Pushed to rescuers when the victim updates their symptoms mid-incident.</summary>
    public const string EventSymptomUpdated = "sos:symptom_updated";
    public const string FcmSymptomUpdateTitleKey = "sos_symptom_update";
    public const string PushSymptomTitle = "Cập nhật triệu chứng!";
    public const string PushSymptomBody = "Nạn nhân ca {0} vừa cập nhật triệu chứng mới. Chạm để xem chi tiết.";
}