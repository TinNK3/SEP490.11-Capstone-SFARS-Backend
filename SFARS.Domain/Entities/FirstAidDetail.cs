using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class FirstAidDetail : BaseEntity
{
    public ToxinGroup ToxinGroup { get; set; } = ToxinGroup.Unknown;
    public Guid? SnakeId { get; set; }
    public int StepOrder { get; set; }
    public string Title { get; set; } = null!;
    public string? ContentMarkdown { get; set; }
    public string? ImageUrl { get; set; }
    public SystemLanguage LanguageCode { get; set; } = SystemLanguage.Vietnamese;

    public virtual Snake? Snake { get; set; } = null!;
}