namespace SFARS.Application.Dtos.FirstAidDetail;

/// <summary>DTO for FirstAidDetail entity used within Application/Service layer.</summary>
public class FirstAidDetailDto
{
    public Guid Id { get; set; }
    public string ToxinGroup { get; set; } = null!;
    public Guid? SnakeId { get; set; }
    public int StepOrder { get; set; }
    public string Title { get; set; } = null!;
    public string? ContentMarkdown { get; set; }
    public string? ImageUrl { get; set; }
    public string LanguageCode { get; set; } = "Vietnamese";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}