using SFARS.Domain.Common.Enum;

namespace SFARS.API.Payloads.Request.Incident
{
    /// <summary>
    /// Request payload for creating an incident (SOS report).
    /// PriorityLevel is NOT user-input — it is auto-determined by the system
    /// based on AI snake detection results.
    /// </summary>
    public class CreateIncidentRequest
    {
        /// <summary>
        /// Latitude of incident location
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Longitude of incident location
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Human-readable address (optional)
        /// </summary>
        public string? AddressString { get; set; }

        /// <summary>
        /// Description of the incident
        /// </summary>
        public string? Description { get; set; }
    }
}