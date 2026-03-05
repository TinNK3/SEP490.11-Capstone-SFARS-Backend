using System.ComponentModel.DataAnnotations;
using SFARS.Domain.Common.Enum;

namespace SFARS.API.Payloads.Request.Incident;

/// <summary>
/// Request payload for analyzing incident media (upload + AI snake detection)
/// </summary>
public class AnalyzeIncidentRequest
{
    /// <summary>
    /// Snake photo to upload and analyze. Can be null if user skips photo.
    /// </summary>
    public IFormFile? File { get; set; }

    /// <summary>
    /// Type of media. Optional if user skips photo.
    /// </summary>
    [EnumDataType(typeof(MediaType), ErrorMessage = "Invalid media type")]
    public MediaType? MediaType { get; set; }
}