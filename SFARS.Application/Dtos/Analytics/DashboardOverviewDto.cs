using System;

namespace SFARS.Application.Dtos.Analytics;

public class DashboardOverviewDto
{
    // Core Counts
    public int TotalIncidents { get; set; }              // All incidents (any status)
    public int ActiveRescues { get; set; }               // Real-time: Pending + Accepted + Reassigned
    public decimal TotalDonations { get; set; }          // Sum of paid transactions (VND)
    public int TotalUsers { get; set; }                  // Active + Inactive + Banned (excluding Deleted)

    // Mission Performance
    public int CompletedRescues { get; set; }            // RescueMission.Status = Completed
    public int FailedRescues { get; set; }               // RescueMission.Status = Rejected

    // Priority & Urgency
    public int CriticalIncidents { get; set; }           // Incident.PriorityLevel = Critical

    // Response Times
    public double AvgResponseTimeMinutes { get; set; }   // Avg(ArrivedAt - StartedAt) in minutes
    public double AvgResolutionTimeMinutes { get; set; } // Avg(CompletedAt - StartedAt) in minutes

    // Performance Metrics
    public double SuccessRate { get; set; }              // (CompletedRescues / TotalRescueMissions) * 100 %

    // Resources & AI
    public int AvailableRescuers { get; set; }           // Online + Active + HasRescuerRole
    public double AiAccuracy { get; set; }               // (ConfirmedCorrect / TotalReviewed) * 100 %
}
