namespace SFARS.Application.Dtos.Analytics;

public class HeatmapDataDto
{
    /// <summary>Latitude coordinate (ICT local)</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude coordinate (ICT local)</summary>
    public double Longitude { get; set; }

    /// <summary>Sum of severity weights for all incidents in this grid cell. Used by heatmap visualization library to determine color intensity.</summary>
    public int Weight { get; set; }

    /// <summary>Total number of incidents in this grid cell</summary>
    public int IncidentCount { get; set; }

    /// <summary>Count of Critical severity incidents in this grid cell</summary>
    public int CriticalCount { get; set; }

    /// <summary>Breakdown of incidents by severity level</summary>
    public SeverityBreakdownDto SeverityBreakdown { get; set; } = new();

    /// <summary>Percentage of incidents resolved (Closed status) in this grid cell</summary>
    public double ResolvedPercent { get; set; }

    /// <summary>Average response time in minutes for completed missions in this grid cell</summary>
    public double AvgResponseTimeMinutes { get; set; }
}

public class SeverityBreakdownDto
{
    public int Critical { get; set; }
    public int High { get; set; }
    public int Medium { get; set; }
    public int Low { get; set; }
}
