namespace SFARS.Infrastructure.Configurations;

public class AgoraOptions
{
    public string AppId { get; set; } = string.Empty;
    public string AppCertificate { get; set; } = string.Empty;
    public int TokenExpirationSeconds { get; set; } = 3600;
}