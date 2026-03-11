using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities
{
    public class AiReviewAuditLog : BaseEntity
    {
        public Guid IncidentId { get; set; }
        public virtual Incident Incident { get; set; } = null!;

        public Guid ReviewId { get; set; }
        public virtual AiInferenceReview Review { get; set; } = null!;

        public Guid RescuerId { get; set; }
        public virtual User Rescuer { get; set; } = null!;

        public AiReviewStatus? OldStatus { get; set; }
        public AiReviewStatus NewStatus { get; set; }
    }
}