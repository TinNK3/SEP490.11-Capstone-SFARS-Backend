using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities
{
    public class SystemMessage : BaseEntity
    {
        public string MsgId { get; set; } = null!; // Logical Key (Code)
        public string MsgContent { get; set; } = null!;
        public string? Vi { get; set; } 
        public string? En { get; set; }
    }
}