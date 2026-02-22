using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class Snake : BaseEntity
{
    public string ScientificName { get; set; } = null!;
    public string CommonName { get; set; } = null!;
    public SnakeRiskLevel ToxicityLevel { get; set; }
    public ToxinGroup ToxinGroup { get; set; } = ToxinGroup.Unknown;
    public string? Description { get; set; }
    public string? KeyIdentifiers { get; set; }
    public string? TypicalSymptoms { get; set; }
    public string? Habitat { get; set; }
    public string? DistributionNote { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<SnakeImage> SnakeImages { get; set; } = new List<SnakeImage>();
    public virtual ICollection<FirstAidDetail> FirstAidDetails { get; set; } = new List<FirstAidDetail>();
}