using SFARS.Domain.Common.Enum;
using SFARS.Application.Dtos.AiInference;

namespace SFARS.Application.Dtos.Incident
{
    public class IncidentDetailDto
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        
        // Location
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? AddressString { get; set; }
        public string? Description { get; set; }

        public IncidentStatus CurrentStatus { get; set; }
        public SeverityLevel PriorityLevel { get; set; }
        
        public double? AiConfidenceScore { get; set; }
        public string? IncidentImage { get; set; }
        
        public DateTime CreatedAt { get; set; }

        // Detail Specific
        public Guid? RescuerId { get; set; }
        public SnakeCandidateDto? PrimarySnake { get; set; }
        public List<SnakeCandidateDto> OtherCandidates { get; set; } = new();
        public WoundAnalysisDto? WoundAnalysis { get; set; }
    }
}