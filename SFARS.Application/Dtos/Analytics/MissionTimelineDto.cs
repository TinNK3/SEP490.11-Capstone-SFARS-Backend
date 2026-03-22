using System;

namespace SFARS.Application.Dtos.Analytics;

public class MissionTimelineDto
{
    /// <summary>When mission was accepted (RescueMission.CreatedAt)</summary>
    public DateTime MissionAcceptedAt { get; set; }
    
    /// <summary>When rescuer started journey (RescueMission.StartedAt)</summary>
    public DateTime? RescueStartedAt { get; set; }
    
    /// <summary>When rescuer arrived at incident (RescueMission.ArrivedAt)</summary>
    public DateTime? RescueArrivedAt { get; set; }
    
    /// <summary>When mission completed (RescueMission.CompletedAt)</summary>
    public DateTime? RescueCompletedAt { get; set; }
}
