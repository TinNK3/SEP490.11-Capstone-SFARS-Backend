using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Mission
{
    public class RescuerTodoMissionDto
    {
        public Guid IncidentId { get; set; }
        public string? IncidentCode { get; set; }
        public Guid MissionId { get; set; }

        public IncidentStatus IncidentStatus { get; set; }
        public AiReviewStatus? ReviewStatus { get; set; }

        public DateTime? MissionStartedAt { get; set; }
        public DateTime? MissionCompletedAt { get; set; }

        public string? PatientName { get; set; }
        public string? Address { get; set; }

        public string? AiPredictedSnakeName { get; set; }
        public bool IsBiteWound { get; set; }
        public string? MediaThumbnailUrl { get; set; }
    }
}