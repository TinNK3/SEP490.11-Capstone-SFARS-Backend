using SFARS.Domain.Common.Enum;
using System.ComponentModel.DataAnnotations;

namespace SFARS.Application.Dtos.Device;

public class RegisterDeviceTokenDto
{
    [Required]
    [MinLength(50, ErrorMessage = "Token length must be at least 50 characters")]
    public string Token { get; set; } = string.Empty;
    
    [Required]
    public DevicePlatform Platform { get; set; }
}