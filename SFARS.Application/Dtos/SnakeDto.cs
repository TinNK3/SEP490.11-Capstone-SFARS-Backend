using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos
{
    public class SnakeDto
    {
        public Guid Id { get; set; }
        public string ScientificName { get; set; } = null!;
        public string CommonName { get; set; } = null!;
        public SnakeRiskLevel ToxicityLevel { get; set; }
        public ToxinGroup ToxinGroup { get; set; }
        public string? Description { get; set; }
        public string? KeyIdentifiers { get; set; }
        public string? TypicalSymptoms { get; set; }
        public string? Habitat { get; set; }
        public string? DistributionNote { get; set; }
        public string? Note { get; set; }
        public bool IsActive { get; set; }
    }
}