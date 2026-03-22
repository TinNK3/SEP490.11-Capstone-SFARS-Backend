using System;

namespace SFARS.Application.Dtos.Analytics;

/// <summary>Geographic location with latitude and longitude</summary>
public class SnakeIncidentLocationDto
{
    /// <summary>Latitude coordinate</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude coordinate</summary>
    public double Longitude { get; set; }
}
