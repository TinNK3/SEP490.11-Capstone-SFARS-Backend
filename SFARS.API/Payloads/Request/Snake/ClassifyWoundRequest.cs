using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SFARS.API.Payloads.Request.Snake;

public class ClassifyWoundRequest
{
    [Required]
    public IFormFile Image { get; set; } = null!;
}