namespace SFARS.Infrastructure.Configurations;

/// <summary>
/// PayOS payment gateway configuration settings
/// </summary>
public class PayOSSettings
{
    public const string SectionName = "PayOSSettings";
    
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public bool AllowSkipSignature { get; set; } = false;
}