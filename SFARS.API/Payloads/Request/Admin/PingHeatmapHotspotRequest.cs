namespace SFARS.API.Payloads.Request.Admin;

public class PingHeatmapHotspotRequest
{
    /// <summary>Latitude of the high-risk area to broadcast (WGS-84)</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude of the high-risk area to broadcast (WGS-84)</summary>
    public double Longitude { get; set; }

    /// <summary>Optional custom notification message override</summary>
    public string? CustomMessage { get; set; }
}
