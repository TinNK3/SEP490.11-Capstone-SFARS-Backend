namespace SFARS.Infrastructure.Configurations;

/// <summary>
/// Cloudinary configuration settings
/// </summary>
public class CloudinarySettings
{
    public const string SectionName = "CloudinarySettings";
    
    public string CloudName { get; set; } = null!;
    public string ApiKey { get; set; } = null!;
    public string ApiSecret { get; set; } = null!;
}