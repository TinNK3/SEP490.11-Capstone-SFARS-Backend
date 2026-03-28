using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SFARS.API.Payloads.Request.Reels;

public class CreateReelRequest
{
    [Required]
    public IFormFile Video { get; set; } = null!;

    [MaxLength(2000)]
    public string? Caption { get; set; }
}

public class CreateReelCommentRequest
{
    [Required]
    [MaxLength(1000)]
    public string Content { get; set; } = null!;

    public Guid? ParentCommentId { get; set; }
}
