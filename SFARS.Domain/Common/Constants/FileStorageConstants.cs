namespace SFARS.Domain.Common.Constants;

/// <summary>
/// Constants for file storage operations and validation
/// </summary>
public static class FileStorageConstants
{
    /// <summary>
    /// Maximum file size for avatar uploads (5 MB)
    /// </summary>
    public const long MaxAvatarSizeBytes = 5 * 1024 * 1024; // 5 MB

    /// <summary>
    /// Maximum file size for incident media uploads (50 MB)
    /// </summary>
    public const long MaxIncidentMediaSizeBytes = 50 * 1024 * 1024; // 50 MB

    /// <summary>
    /// Allowed MIME types for avatar uploads
    /// </summary>
    public static readonly string[] AllowedAvatarMimeTypes =
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    /// <summary>
    /// Allowed MIME types for incident media (images and videos)
    /// </summary>
    public static readonly string[] AllowedIncidentMediaMimeTypes =
    {
        // Images
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
        // Videos
        "video/mp4",
        "video/quicktime",
        "video/x-msvideo"
    };

    /// <summary>
    /// Cloudinary folder for avatar uploads
    /// </summary>
    public const string AvatarFolder = "user_avatars";

    /// <summary>
    /// Cloudinary folder for incident media
    /// </summary>
    public const string IncidentMediaFolder = "incident_media";
}
