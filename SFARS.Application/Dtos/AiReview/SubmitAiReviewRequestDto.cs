using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.AiReview
{
    public class SubmitAiReviewRequestDto
    {
        public AiReviewStatus ReviewStatus { get; set; }

        public Guid? CorrectedSnakeId { get; set; }
        
        public ToxinGroup? CorrectedToxinGroup { get; set; }

        public UnableToAssessReason? UnableToAssessReasonChoice { get; set; }

        public string? Comment { get; set; }
    }
}