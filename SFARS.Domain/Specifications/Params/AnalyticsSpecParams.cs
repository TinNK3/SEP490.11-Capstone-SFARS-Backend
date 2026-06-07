using System;

namespace SFARS.Domain.Specifications.Params;

public class AnalyticsSpecParams : BaseSpecParams
{
    public Guid? RescuerId { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    // Heatmap bounding box – only incidents inside the visible map viewport are returned.
    // All are optional; omitting them = return all (up to MaxHeatmapPoints).
    public double? MinLat { get; set; }
    public double? MaxLat { get; set; }
    public double? MinLng { get; set; }
    public double? MaxLng { get; set; }

    /// <summary>
    /// Map zoom level (0–20). Higher zoom = smaller cluster radius.
    /// Used by server-side geospatial clustering in the Heatmap endpoint.
    /// </summary>
    public int? Zoom { get; set; }
}
