using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;
namespace SFARS.Domain.Entities
{
    public class IncidentChatMessage : BaseEntity
    {
        public Guid ChatId { get; set; }
        public ChatSenderType SenderType { get; set; }
        public Guid? SenderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? ModelName { get; set; }
        public string? ModelVersion { get; set; }
        public string? MetadataJson { get; set; }
        public Guid? AiInferenceId { get; set; }

        // Navigation
        public virtual IncidentChat Chat { get; set; } = null!;
        public virtual User? Sender { get; set; }
        public virtual AiInference? AiInference { get; set; }

        public virtual ICollection<IncidentChatMessageMedia> Medias { get; set; } = new List<IncidentChatMessageMedia>();
    }
}