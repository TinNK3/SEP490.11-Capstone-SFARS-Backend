using System;
using System.Collections.Generic;

namespace SFARS.Application.Dtos.Analytics;

public class SnakeIncidentTrackingDto
{
    /// <summary>Unique identifier of the snake species</summary>
    public Guid SnakeId { get; set; }

    /// <summary>Common/Vietnamese name of the snake (e.g., "Rắn hổ mang")</summary>
    public string SnakeName { get; set; } = null!;

    /// <summary>Scientific name of the snake (e.g., "Naja naja")</summary>
    public string ScientificName { get; set; } = null!;

    /// <summary>Toxicity level enum value (VeryLow, Low, Moderate, High, VeryHigh)</summary>
    public string ToxicityLevel { get; set; } = null!;

    /// <summary>Toxin group (Neurotoxin, Hemotoxin, Cytotoxin, Myotoxin, Unknown)</summary>
    public string ToxinGroup { get; set; } = null!;

    /// <summary>Total number of incidents where AI identified this snake</summary>
    public int TotalIncidentsIdentified { get; set; }

    /// <summary>
    /// Accuracy rate: Count of incidents where rescuer confirmed the AI identification 
    /// divided by total incidents identified (0-1 range)
    /// </summary>
    public double AccuracyRate { get; set; }

    /// <summary>List of all incidents where this snake was identified</summary>
    public List<SnakeIncidentDetailDto> Incidents { get; set; } = new();
}
