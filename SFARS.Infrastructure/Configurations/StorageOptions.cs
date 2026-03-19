namespace SFARS.Infrastructure.Configurations;

/// <summary>
/// Storage configuration for file uploads
/// </summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Maximum file upload size in bytes (default: 10MB)
    /// </summary>
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Folder format for incident media. {0} = incidentId
    /// </summary>
    public string IncidentMediaFolderFormat { get; set; } = "incidents/{0}/media";

    /// <summary>
    /// Allowed image MIME types
    /// </summary>
    public string[] AllowedImageTypes { get; set; } = new[]
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
    };

    /// <summary>
    /// Allowed video MIME types
    /// </summary>
    public string[] AllowedVideoTypes { get; set; } = new[]
    {
        "video/mp4",
        "video/quicktime",
        "video/x-msvideo"
    };
}