using System.ComponentModel.DataAnnotations;
using SFARS.Domain.Common.Enum;

namespace SFARS.API.Payloads.Request.Incident;

/// <summary>
/// Request payload for uploading incident media
/// </summary>
public class UploadIncidentMediaRequest
{
    /// <summary>
    /// File to upload (image/video)
    /// </summary>
    [Required(ErrorMessage = "File is required")]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Type of media (SnakePhoto, BiteWoundPhoto, Other)
    /// </summary>
    [Required]
    [EnumDataType(typeof(MediaType), ErrorMessage = "Invalid media type")]
    public MediaType MediaType { get; set; } = MediaType.SnakePhoto;
}