using System.ComponentModel.DataAnnotations;
using SFARS.Domain.Common.Enum;

namespace SFARS.API.Payloads.Request.Incident;

/// <summary>
/// Request payload for analyzing incident media (upload + AI snake detection)
/// </summary>
public class AnalyzeIncidentRequest
{
    /// <summary>
    /// Snake photo to upload and analyze
    /// </summary>
    [Required(ErrorMessage = "File is required")]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Type of media (default: SnakePhoto)
    /// </summary>
    [Required]
    [EnumDataType(typeof(MediaType), ErrorMessage = "Invalid media type")]
    public MediaType MediaType { get; set; } = MediaType.SnakePhoto;
}