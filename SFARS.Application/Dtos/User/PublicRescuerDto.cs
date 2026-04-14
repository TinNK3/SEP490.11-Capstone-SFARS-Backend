using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.User;

/// <summary>
/// Public-facing rescuer DTO for listing available rescuers.
/// Contains aggregated metrics like TotalMissions.
/// </summary>
public class PublicRescuerDto
{
    public Guid Id { get; set; }
    
    // Identity Info
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string FullName => $"{FirstName} {LastName}";
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Avatar { get; set; }
    
    // Medical / Profile
    public string? Address { get; set; }
    public Gender? Gender { get; set; }
    public DateTime? Dob { get; set; }
    
    // Status & System
    public UserStatus Status { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Rescuer Info
    public int TotalMissions { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}