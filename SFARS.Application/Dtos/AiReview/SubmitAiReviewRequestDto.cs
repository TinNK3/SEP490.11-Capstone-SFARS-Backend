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

        /// <summary>
        /// For BiteWoundPhoto review: Reviewer confirms if the wound is a snake bite.
        /// Null for SnakePhoto reviews.
        /// </summary>
        public bool? IsConfirmedSnakeBite { get; set; }

        // Snake Image Upload (for new/unknown snakes)
        public string? NewSnakeCommonName { get; set; }
    }
}