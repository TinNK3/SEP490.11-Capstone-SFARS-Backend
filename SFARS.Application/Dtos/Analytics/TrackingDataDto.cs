using System;
using System.Collections.Generic;

namespace SFARS.Application.Dtos.Analytics;

public class TrackingDataDto
{
    /// <summary>Total GPS tracking checkpoints recorded</summary>
    public int TotalCheckpoints { get; set; }
    
    /// <summary>Average speed during journey in km/h</summary>
    public double AvgSpeed { get; set; }
    
    /// <summary>Maximum speed reached in km/h</summary>
    public double MaxSpeed { get; set; }
    
    /// <summary>GPS tracking points from dispatch to incident</summary>
    public List<TrackingPointDto> TrackingPoints { get; set; } = new();
}

public class TrackingPointDto
{
    /// <summary>When this checkpoint was recorded (UTC)</summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>Latitude at this point</summary>
    public double Latitude { get; set; }
    
    /// <summary>Longitude at this point</summary>
    public double Longitude { get; set; }
    
    /// <summary>Speed at this point in km/h</summary>
    public double? Speed { get; set; }
    
    /// <summary>GPS accuracy in meters</summary>
    public double? Accuracy { get; set; }
}
