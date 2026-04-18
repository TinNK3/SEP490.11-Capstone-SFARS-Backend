using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Incident
{
    public class IncidentDto
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        
        // Location
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? AddressString { get; set; }
        
        // Details
        public string? Description { get; set; }
        public int? MinutesSinceBite { get; set; }
        public IncidentStatus CurrentStatus { get; set; } = IncidentStatus.Pending;
        public SeverityLevel PriorityLevel { get; set; } = SeverityLevel.Medium;
        
        // AI Prediction
        public double? AiConfidenceScore { get; set; }
        public string? IncidentImage { get; set; }
        
        // AI Review Snapshot
        public bool IsVerified { get; set; }
        public AiReviewStatus? CurrentAiReviewStatus { get; set; }
        
        // Relations
        public Guid VictimId { get; set; }
        public string? VictimName { get; set; }
        
        // Audit
        public DateTime CreatedAt { get; set; }
    }
}