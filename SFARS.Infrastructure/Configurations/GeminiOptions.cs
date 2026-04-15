namespace SFARS.Infrastructure.Configurations;

/// <summary>
/// Configuration for Gemini AI service
/// </summary>
public class GeminiOptions
{
    public string Model { get; set; } = "gemini-2.0-flash";
    public string EmbeddingModel { get; set; } = "text-embedding-004";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
    public double Temperature { get; set; } = 0.3;
    public int MaxRetries { get; set; } = 2;
    public int TimeoutSeconds { get; set; } = 30;
}