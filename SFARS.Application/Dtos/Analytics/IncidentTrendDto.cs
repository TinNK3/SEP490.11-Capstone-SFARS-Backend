using System;

namespace SFARS.Application.Dtos.Analytics;

public class IncidentTrendDto
{
    /// <summary>Date (ICT local date)</summary>
    public DateTime Date { get; set; }

    /// <summary>Total incidents created on this date</summary>
    public int TotalIncidents { get; set; }

    /// <summary>Count of Critical severity incidents</summary>
    public int CriticalCount { get; set; }

    /// <summary>Percentage of incidents resolved (Closed status) on this date</summary>
    public double ResolvedPercent { get; set; }

    /// <summary>Average response time in minutes (StartedAt → ArrivedAt) for completed missions</summary>
    public double AvgResponseTimeMinutes { get; set; }

    /// <summary>AI accuracy percentage: (ConfirmedCorrect reviews) / (total non-pending reviews) * 100</summary>
    public double AiAccuracy { get; set; }
}
