namespace SFARS.Application.Dtos.Analytics;

public class MissionDetailsDto
{
    /// <summary>Rescuer's observations and notes</summary>
    public string? RescuerNotes { get; set; }
    
    public string? PatientConditionAtHandover { get; set; }
}
