using System.ComponentModel.DataAnnotations;

namespace SFARS.API.Payloads.Request.Admin;

public class CreateGeminiKeyRequest
{
    [Required(ErrorMessage = "API Key value is required")]
    [MaxLength(256)]
    public string KeyValue { get; set; } = null!;

    [MaxLength(200)]
    public string? Label { get; set; }
}

public class ToggleGeminiKeyRequest
{
    public bool IsActive { get; set; }
}