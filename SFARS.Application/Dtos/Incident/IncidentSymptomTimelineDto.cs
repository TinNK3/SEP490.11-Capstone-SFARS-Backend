using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Incident
{
    /// <summary>
    /// Represents a single time-series snapshot of the victim's symptoms.
    /// A list of these DTOs allows the client to draw a clinical chart over time.
    /// </summary>
    public class IncidentSymptomTimelineDto
    {
        public Guid Id { get; set; }
        
        /// <summary>
        /// The exact time when this specific symptom update was submitted.
        /// </summary>
        public DateTime ReportedAt { get; set; }
        
        /// <summary>
        /// List of active clinically mapped symptoms at this specific timestamp.
        /// </summary>
        public List<SymptomType> ActiveSymptoms { get; set; } = new();
        
        /// <summary>
        /// The raw string of symptoms sent by the FE (e.g. "Chảy máu ồ ạt, Sụp mí")
        /// </summary>
        public string? Notes { get; set; }
    }
}