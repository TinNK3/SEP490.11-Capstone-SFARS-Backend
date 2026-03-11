using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.AiReview;

public class AiReviewCompletedNotificationDto
{
    public Guid IncidentId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = string.Empty;
    public string EffectiveToxinGroup { get; set; } = string.Empty;
    public bool RequiresFirstAidRefresh { get; set; }
}