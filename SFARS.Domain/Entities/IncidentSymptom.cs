using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities
{
    public class IncidentSymptom : BaseEntity
    {
        public Guid IncidentId { get; set; }
        public Guid? ReportedBy { get; set; }
        public bool HasBleeding { get; set; }
        public bool HasSwelling { get; set; }
        public bool HasNecrosis { get; set; }
        public bool HasBreathingDifficulty { get; set; }
        public bool HasPtosis { get; set; }     // sụp mí
        public bool HasVomiting { get; set; }
        public bool HasPain { get; set; }

        public string? Notes { get; set; }

        public DateTime ReportedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual Incident Incident { get; set; } = null!;
        public virtual User? Reporter { get; set; }
    }
}