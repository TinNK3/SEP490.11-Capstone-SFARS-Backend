using System;

namespace SFARS.Application.Dtos.Analytics;

public class RescuerPerformanceDto
{
    public Guid RescuerId { get; set; }
    public string RescuerName { get; set; } = null!;
    public int TotalMissions { get; set; }
    public double SuccessRate { get; set; }
    public double AvgResponseTimeMinutes { get; set; }
}
