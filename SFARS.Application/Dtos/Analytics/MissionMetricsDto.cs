namespace SFARS.Application.Dtos.Analytics;

public class MissionMetricsDto
{
    /// <summary>Response time in minutes (StartedAt to ArrivedAt)</summary>
    public double ResponseTimeMinutes { get; set; }
    
    /// <summary>Handover time in minutes (ArrivedAt to CompletedAt)</summary>
    public double HandoverTimeMinutes { get; set; }
    
    /// <summary>Total mission duration in minutes (CreatedAt to CompletedAt)</summary>
    public double TotalDurationMinutes { get; set; }
}
