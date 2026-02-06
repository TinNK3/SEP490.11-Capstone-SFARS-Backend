using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities
{
    public class IncidentChat : BaseEntity
    {
        public Guid IncidentId { get; set; }
        public string? Title { get; set; }
        public DateTime? LastMessageAt { get; set; }

        // Navigation
        public virtual Incident Incident { get; set; } = null!;
        public virtual ICollection<IncidentChatMessage> Messages { get; set; } = new List<IncidentChatMessage>();
    }
}