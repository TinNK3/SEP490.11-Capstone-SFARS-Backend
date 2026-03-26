namespace SFARS.Application.Dtos.Analytics;

public class PingHeatmapHotspotDto
{
    /// <summary>Latitude of the high-risk hotspot (WGS-84)</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude of the high-risk hotspot (WGS-84)</summary>
    public double Longitude { get; set; }

    /// <summary>Optional custom alert message. If null, a default message is generated.</summary>
    public string? CustomMessage { get; set; }
}
