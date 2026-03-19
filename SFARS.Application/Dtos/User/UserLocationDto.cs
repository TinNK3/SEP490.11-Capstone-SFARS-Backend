namespace SFARS.Application.Dtos.User;

public class UserLocationDto
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? LocationUpdatedAt { get; set; }
    public double? AccuracyMeters { get; set; }
    public string AccuracyLevel { get; set; } = "unknown";
}