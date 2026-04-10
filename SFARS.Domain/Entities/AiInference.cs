using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities
{
    public class AiInference : BaseEntity
    {
        public Guid IncidentId { get; set; }
        public virtual Incident Incident { get; set; } = null!;
        public Guid? IncidentMediaId { get; set; }
        public virtual IncidentMedia? IncidentMedia { get; set; }
        public string? ModelName { get; set; }     // "snake-cls-v1"
        public string? ModelVersion { get; set; }  // "1.0.0"
        public int TopK { get; set; } = 3;
        public Guid? SelectedSnakeId { get; set; }
        public virtual Snake? SelectedSnake { get; set; }
        public double? SelectedConfidence { get; set; }

        public ToxinGroup SelectedToxinGroup { get; set; } = ToxinGroup.Unknown;

        public string? DecisionRule { get; set; } // "Top1Only" / "Top1>=0.7"...

        public virtual ICollection<AiInferenceCandidate> Candidates { get; set; } = new List<AiInferenceCandidate>();
        
        /// <summary>
        /// True if the user uploaded a Wound Photo and the model classified it as a Snake Bite.
        /// False if classified as Not a Snake Bite. Null if the user uploaded a Snake Photo instead.
        /// </summary>
        public bool? IsSnakeBite { get; set; }
    }
}