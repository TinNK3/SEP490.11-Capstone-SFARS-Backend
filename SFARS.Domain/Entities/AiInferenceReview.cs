using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities
{
    /// <summary>
    /// Rescuer's review / override of an AI snake identification result.
    /// One rescuer can have at most 1 review per AiInference (upsert pattern).
    /// </summary>
    public class AiInferenceReview : BaseEntity
    {
        public Guid AiInferenceId { get; set; }
        public virtual AiInference AiInference { get; set; } = null!;

        public Guid IncidentId { get; set; }
        public virtual Incident Incident { get; set; } = null!;

        /// <summary>Rescuer who submitted this review</summary>
        public Guid ReviewerId { get; set; }
        public virtual User Reviewer { get; set; } = null!;

        public AiReviewStatus ReviewStatus { get; set; } = AiReviewStatus.Pending;

        /// <summary>
        /// Filled only when ReviewStatus == Corrected.
        /// The snake ID that the rescuer believes is the correct identification.
        /// </summary>
        public Guid? CorrectedSnakeId { get; set; }
        public virtual Snake? CorrectedSnake { get; set; }

        public ToxinGroup? CorrectedToxinGroup { get; set; }

        public UnableToAssessReason? UnableToAssessReasonChoice { get; set; }

        /// <summary>Optional free-text note from rescuer</summary>
        public string? Comment { get; set; }

        public DateTime? ReviewedAt { get; set; }
    }
}