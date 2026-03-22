namespace SFARS.Application.Dtos.Analytics;

public class MissionDetailsDto
{
    /// <summary>Rescuer's observations and notes</summary>
    public string? RescuerNotes { get; set; }
    
    /// <summary>Patient condition when handed over</summary>
    public string? PatientConditionAtHandover { get; set; }
    
    /// <summary>Victim's rating of rescuer performance (1-5)</summary>
    public int? VictimRating { get; set; }
    
    /// <summary>Victim's comment about rescuer</summary>
    public string? VictimComment { get; set; }
}
