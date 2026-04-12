using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Incident
{
    public class SnakePredictionDto
    {
        public Guid SnakeId { get; set; }
        public string ScientificName { get; set; } = null!;
        public string CommonName { get; set; } = null!;
        public double Confidence { get; set; }
        public SnakeRiskLevel ToxicityLevel { get; set; }
        public ToxinGroup ToxinGroup { get; set; }
        public string DangerSummary { get; set; } = null!;
        public string? TypicalSymptoms { get; set; }
    }
}