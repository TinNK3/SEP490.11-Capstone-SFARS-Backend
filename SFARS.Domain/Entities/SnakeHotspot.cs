using NetTopologySuite.Geometries;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class SnakeHotspot : BaseEntity
{
    public Guid ReporterId { get; set; }
    public Point Location { get; set; } = null!;
    public Guid? SnakeId { get; set; }
    public SeverityLevel RiskLevel { get; set; }
    public int Upvotes { get; set; }
    public bool VerifiedByExpert { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public HotspotObservationType ObservationType { get; set; } = HotspotObservationType.Unknown;
    public ToxinGroup? ToxinGroup { get; set; }

    public virtual User Reporter { get; set; } = null!;
    public virtual Snake? Snake { get; set; }
}