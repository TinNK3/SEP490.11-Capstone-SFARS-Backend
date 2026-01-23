using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace SFARS.Domain.Entities;

public class RescueTrackingLog
{
    [Key]
    public long Id { get; set; }
    public Guid MissionId { get; set; }
    public Guid RescuerId { get; set; }
    public Point Location { get; set; } = null!;
    public double? SpeedKMH { get; set; }
    public double? AccuracyMeters { get; set; }
    public int? BatteryLevel { get; set; }
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    public virtual RescueMission Mission { get; set; } = null!;
}