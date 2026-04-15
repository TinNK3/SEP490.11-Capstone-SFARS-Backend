using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class QuizQuestion : BaseEntity
{
    public Guid QuizId { get; set; }
    public string Content { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public QuestionType QuestionType { get; set; }
    public string? Explanation { get; set; }
    public int Order { get; set; }

    // Navigation Properties
    public virtual Quiz Quiz { get; set; } = null!;
    public virtual ICollection<QuizOption> Options { get; set; } = new List<QuizOption>();
}
