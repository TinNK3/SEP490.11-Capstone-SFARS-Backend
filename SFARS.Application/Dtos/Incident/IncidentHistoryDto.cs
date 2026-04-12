using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Incident
{
    public class IncidentHistoryDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = null!;
        public IncidentStatus CurrentStatus { get; set; }
        public SeverityLevel PriorityLevel { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? IncidentImage { get; set; }
    }
}