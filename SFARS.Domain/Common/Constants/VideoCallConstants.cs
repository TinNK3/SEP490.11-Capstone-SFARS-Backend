namespace SFARS.Domain.Common.Constants;

public static class VideoCallConstants
{
    // Agora Configuration Keys
    public const string AgoraSection = "AgoraService";
    public const string AppId = "AppId";
    public const string AppCertificate = "AppCertificate";
    public const string TokenExpirationSeconds = "TokenExpirationSeconds";

    // SignalR Events
    public const string EventCallIncoming = "CallIncoming";
    public const string EventCallAccepted = "CallAccepted";
    public const string EventCallRejected = "CallRejected";
    public const string EventCallEnded = "CallEnded";

    // FCM Call Data
    public const string FcmCallTypeKey = "type";
    public const string FcmCallTypeValue = "VIDEO_CALL_INCOMING";
    public const string FcmIncidentIdKey = "incidentId";
    public const string FcmCallerNameKey = "callerName";

    // Localized UI Labels (Note: In a multi-language setup, these could be move to .resx)
    public const string LabelCallerRescuer = "Cán bộ cứu hộ";
    public const string LabelCallerVictim = "Nạn nhân";
    public const string PushTitle = "Cuộc gọi cứu hộ khẩn cấp";
    public const string PushBodyTemplate = "Đang có cuộc gọi từ {0}...";
}