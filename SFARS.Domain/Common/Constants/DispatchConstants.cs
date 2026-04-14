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

    /// <summary>
    /// Per-user dispatch group: "dispatch:user:{userId}".
    /// DispatchService pushes to this group so only that rescuer receives the event.
    /// </summary>
    public const string RescuerGroupPrefix = "dispatch:user:";

    /// <summary>
    /// Global group for all connected and verified rescuers.
    /// Used for broadcasting community-wide incident updates.
    /// </summary>
    public const string AllRescuersGroup = "dispatch:rescuers:all";

    /// <summary>Pushed to rescuers when a new SOS is dispatched in their tier.</summary>
    public const string EventNewDispatch   = "sos:dispatch";

    /// <summary>Pushed to the global rescuers group when a community incident is created or status changes.</summary>
    public const string EventCommunityIncidentUpdated = "sos:community_updated";

    /// <summary>Pushed to the victim when all tiers fail — triggers fallback UI.</summary>
    public const string EventFallback      = "sos:fallback";

    /// <summary>Pushed to all rescuers in range when one has already accepted — dismiss card.</summary>
    public const string EventAssigned      = "sos:assigned";

    /// <summary>Pushed privately to rescuer when their Location pings trigger the Arrived Geofence.</summary>
    public const string EventSuggestArrived = "sos:suggest_arrived";

    /// <summary>Pushed to rescuers when the victim re-analyzes (retakes photo) during an active incident.</summary>
    public const string EventAiUpdated = "sos:ai_updated";

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

    // AI Re-Analyze — SignalR + FCM
    /// <summary>FCM data key when victim retakes a photo during an active incident.</summary>
    public const string FcmAiReanalyzeTitleKey = "sos_ai_reanalysis";
    public const string PushAiReanalyzeTitle = "Cập nhật nhận diện AI!";
    public const string PushAiReanalyzeBody = "Nạn nhân ca {0} đã chụp lại ảnh. Kết quả mới: {1}. Chạm để xem.";

    // Suggest Arrived — SignalR + FCM
    /// <summary>FCM data key when geofence detects rescuer near incident location.</summary>
    public const string FcmSuggestArrivedTitleKey = "sos_suggest_arrived";
    public const string PushSuggestArrivedTitle = "Bạn đã đến nơi?";
    public const string PushSuggestArrivedBody = "Hệ thống nhận thấy bạn đang ở gần vị trí sự cố. Chạm để xác nhận Đã Tới.";

    // Mission Status — FCM to victim
    /// <summary>FCM data key when rescuer updates mission status (Arrived/Closed).</summary>
    public const string FcmMissionStatusTitleKey = "sos_mission_status";
    public const string PushMissionArrivedTitle = "Cứu hộ đã tới nơi!";
    public const string PushMissionArrivedBody = "Nhân viên cứu hộ đã đến vị trí của bạn.";
    public const string PushMissionClosedTitle = "Ca cấp cứu đã đóng";
    public const string PushMissionClosedBody = "Ca cấp cứu đã được hoàn tất. Cảm ơn bạn đã sử dụng SFARS.";

    // SMS Fallback Constants
    public const string SmsSosPrefix = "SFARS SOS";
    public const string SmsAddressFallback = "Khu vực không xác định (Báo cáo qua SMS Ngoại tuyến)";
    public const string SmsDescriptionFallback = "Tín hiệu SOS gửi tự động qua SMS khi nạn nhân mất kết nối mạng. Hãy chủ động liên lạc qua cuộc gọi thoại.";
}