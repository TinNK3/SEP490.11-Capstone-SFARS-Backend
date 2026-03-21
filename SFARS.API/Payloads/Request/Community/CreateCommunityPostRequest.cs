using Microsoft.AspNetCore.Http;

namespace SFARS.API.Payloads.Request.Community;

public class CreateCommunityPostRequest
{
    public string? Content { get; set; }
    public List<IFormFile>? MediaFiles { get; set; }
}
