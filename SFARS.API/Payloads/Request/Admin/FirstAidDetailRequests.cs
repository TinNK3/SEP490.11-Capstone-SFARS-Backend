using SFARS.Domain.Common.Enum;

namespace SFARS.API.Payloads.Request.Admin;

/// <summary>
/// Unified request payload for creating or updating a First Aid Detail step.
/// </summary>
public class UpsertFirstAidDetailRequest
{
    public ToxinGroup ToxinGroup { get; set; }
    public int StepOrder { get; set; }
    public string Title { get; set; } = null!;
    public string? ContentMarkdown { get; set; }
}