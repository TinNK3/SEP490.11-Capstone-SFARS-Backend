using Microsoft.AspNetCore.Http;

namespace SFARS.API.Payloads.Request.Community;

public class UpdateCommunityPostRequest
{
    public string? Content { get; set; }
    public List<string>? RetainedMediaUrls { get; set; }
    public List<IFormFile>? NewMediaFiles { get; set; }
}
