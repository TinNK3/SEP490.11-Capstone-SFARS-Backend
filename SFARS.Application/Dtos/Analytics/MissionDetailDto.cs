using System;
using System.Collections.Generic;

namespace SFARS.Application.Dtos.Analytics;

public class MissionDetailDto
{
    public Guid MissionId { get; set; }
    public string IncidentCode { get; set; } = null!;
    public string VictimName { get; set; } = null!;
    public string? VictimPhone { get; set; }
    public string? VictimLocation { get; set; }
    
    public MissionLocationDto IncidentLocation { get; set; } = new();
    public string Severity { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    
    public MissionTimelineDto Timeline { get; set; } = new();
    public MissionMetricsDto Metrics { get; set; } = new();
    public MissionDetailsDto Details { get; set; } = new();
    public TrackingDataDto TrackingData { get; set; } = new();
}

public class MissionLocationDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
