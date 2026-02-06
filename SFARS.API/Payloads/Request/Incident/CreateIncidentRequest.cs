using SFARS.Domain.Common.Enum;

namespace SFARS.API.Payloads.Request.Incident
{
    /// <summary>
    /// Request payload for creating an incident (SOS report)
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

        /// <summary>
        /// Priority level (default: Unknown)
        /// </summary>
        public SeverityLevel PriorityLevel { get; set; } = SeverityLevel.Unknown;
    }
}