using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class QuizResult : BaseEntity
{
    public Guid QuizHistoryId { get; set; }
    public Guid QuestionId { get; set; }
    public bool IsCorrect { get; set; }
    
    /// <summary>
    /// The ID of the option selected for MCQ/TrueFalse.
    /// </summary>
    public Guid? SelectedOptionId { get; set; }

    /// <summary>
    /// JSON data for complex answer types (like Ordering).
    /// </summary>
    public string? AnswerData { get; set; }

    // Navigation Properties
    public virtual QuizHistory QuizHistory { get; set; } = null!;
    public virtual QuizQuestion Question { get; set; } = null!;
}
