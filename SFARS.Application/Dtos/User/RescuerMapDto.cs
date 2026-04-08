namespace SFARS.Application.Dtos.User;

/// <summary>
/// Optimized DTO for map visualization.
/// minimal fields to reduce bandwidth when fetching hundreds of rescuers.
/// </summary>
public class RescuerMapDto
{
    public Guid Id { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}