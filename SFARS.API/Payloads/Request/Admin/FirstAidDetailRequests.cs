namespace SFARS.API.Payloads.Request.Admin;

/// <summary>
/// Request payload for creating a new First Aid Detail step.
/// </summary>
public class CreateFirstAidDetailRequest
{
    public string ToxinGroup { get; set; } = null!;
    public Guid? SnakeId { get; set; }
    public int StepOrder { get; set; }
    public string Title { get; set; } = null!;
    public string? ContentMarkdown { get; set; }
    public string? ImageUrl { get; set; }
    public string LanguageCode { get; set; } = "Vietnamese";
}

/// <summary>
/// Request payload for updating an existing First Aid Detail step.
/// </summary>
public class UpdateFirstAidDetailRequest
{
    public string ToxinGroup { get; set; } = null!;
    public Guid? SnakeId { get; set; }
    public int StepOrder { get; set; }
    public string Title { get; set; } = null!;
    public string? ContentMarkdown { get; set; }
    public string? ImageUrl { get; set; }
    public string LanguageCode { get; set; } = "Vietnamese";
}