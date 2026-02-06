using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities
{
    public class AiInferenceCandidate : BaseEntity
    {
        public Guid AiInferenceId { get; set; }
        public virtual AiInference AiInference { get; set; } = null!;

        public int Rank { get; set; } // 1..3
        public Guid SnakeId { get; set; }
        public virtual Snake Snake { get; set; } = null!;

        public double Confidence { get; set; }
    }
}