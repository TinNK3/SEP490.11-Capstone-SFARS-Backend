using SFARS.Application.Dtos.AiInference;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Incident
{
    public class AdminIncidentDetailDto
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }

        public LocationCoords Patient { get; set; } = null!;
        public LocationCoords? Rescuer { get; set; }

        public string? AddressString { get; set; }
        public string? Description { get; set; }
        public IncidentStatus CurrentStatus { get; set; }
        public SeverityLevel PriorityLevel { get; set; }

        public string? IncidentImage { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? RescuerId { get; set; }

        // AI Results & Review Data Separated for Admin
        public OriginalAiPredictionDto? OriginalAiPrediction { get; set; }
        public RescuerReviewDataDto? RescuerReviewData { get; set; }
        public AdminReviewDataDto? AdminReviewData { get; set; }
    }

    public class OriginalAiPredictionDto
    {
        public Guid AiInferenceId { get; set; }
        public double? AiConfidenceScore { get; set; }
        public WoundAnalysisDto? WoundAnalysis { get; set; }
        public SnakeCandidateDto? PrimarySnake { get; set; }
        public List<SnakeCandidateDto>? OtherCandidates { get; set; }
    }

    public class RescuerReviewDataDto
    {
        public Guid ReviewerId { get; set; }
        public string? ReviewerName { get; set; }
        public AiReviewStatus ReviewStatus { get; set; }
        
        public Guid? CorrectedSnakeId { get; set; }
        public string? CorrectedSnakeName { get; set; }
        public ToxinGroup? CorrectedToxinGroup { get; set; }
        
        public bool? IsConfirmedWoundSnakeBite { get; set; }
        public UnableToAssessReason? UnableToAssessReasonChoice { get; set; }
        public string? RescuerComment { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }

    public class AdminReviewDataDto
    {
        public Guid AdminReviewerId { get; set; }
        public string? AdminReviewerName { get; set; }
        public AiReviewStatus ReviewStatus { get; set; }

        public Guid? CorrectedSnakeId { get; set; }
        public string? CorrectedSnakeName { get; set; }
        public ToxinGroup? CorrectedToxinGroup { get; set; }

        public bool? IsConfirmedWoundSnakeBite { get; set; }
        public UnableToAssessReason? UnableToAssessReasonChoice { get; set; }

        public string? AdminComment { get; set; }
        public DateTime? AdminReviewedAt { get; set; }
    }
}