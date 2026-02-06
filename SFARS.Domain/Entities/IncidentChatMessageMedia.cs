using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities
{
    public class IncidentChatMessageMedia : BaseEntity
    {
        public Guid MessageId { get; set; }
        public Guid? IncidentMediaId { get; set; }
        public string? MediaUrl { get; set; }

        // Navigation
        public virtual IncidentChatMessage Message { get; set; } = null!;
        public virtual IncidentMedia? IncidentMedia { get; set; }
    }
}