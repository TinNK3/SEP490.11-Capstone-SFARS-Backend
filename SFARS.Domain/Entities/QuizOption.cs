using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class QuizOption : BaseEntity
{
    public Guid QuestionId { get; set; }
    public string Content { get; set; } = null!;
    public bool IsCorrect { get; set; }
    
    /// <summary>
    /// Correct position in ordering questions (1, 2, 3...). 
    /// Null for MultipleChoice/TrueFalse.
    /// </summary>
    public int? StepOrder { get; set; }
    
    /// <summary>
    /// Message shown if this "Trap" option is selected.
    /// </summary>
    public string? PenaltyNote { get; set; }

    // Navigation Properties
    public virtual QuizQuestion Question { get; set; } = null!;
}
