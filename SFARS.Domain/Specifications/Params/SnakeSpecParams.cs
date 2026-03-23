using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Specifications.Params
{
    public class SnakeSpecParams : BaseSpecParams
    {
        public string? ToxicLevel { get; set; } // Map carefully if string or enum is used
        public SnakeRiskLevel? ToxicityLevel { get; set; }
        public ToxinGroup? ToxinGroup { get; set; }
        public bool? IsActive { get; set; }
    }
}