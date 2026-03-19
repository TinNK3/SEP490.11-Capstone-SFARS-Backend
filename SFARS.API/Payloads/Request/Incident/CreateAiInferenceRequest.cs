using System.ComponentModel.DataAnnotations;

namespace SFARS.API.Payloads.Request.Incident;

/// <summary>
/// Request to create AI inference for incident media
/// </summary>
public class CreateAiInferenceRequest
{
    /// <summary>
    /// ID of the incident media to analyze (must be photo)
    /// </summary>
    [Required(ErrorMessage = "IncidentMediaId is required")]
    public Guid IncidentMediaId { get; set; }
}