using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SFARS.API.Payloads.Request.Snake;

public class IdentifySnakeRequest
{
    [Required(ErrorMessage = "Please attach an image for identification.")]
    public IFormFile Image { get; set; } = null!;
}