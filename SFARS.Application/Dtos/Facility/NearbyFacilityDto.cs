namespace SFARS.Application.Dtos.Facility;

public class NearbyFacilityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? OperatingHours { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceMeters { get; set; }
}