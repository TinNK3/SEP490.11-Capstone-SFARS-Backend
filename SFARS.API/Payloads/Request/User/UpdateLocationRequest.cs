using System.ComponentModel.DataAnnotations;

namespace SFARS.API.Payloads.Request.User;

public class UpdateLocationRequest
{
    [Required]
    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90")]
    public double Latitude { get; set; }

    [Required]
    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180")]
    public double Longitude { get; set; }

    [Range(0, 10000, ErrorMessage = "AccuracyMeters must be between 0 and 10000")]
    public double? AccuracyMeters { get; set; }
}