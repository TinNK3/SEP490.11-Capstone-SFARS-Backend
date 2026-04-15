using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class QuizHistory : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid QuizId { get; set; }
    public int Score { get; set; }
    public int TotalQuestions { get; set; }
    public int PointsEarned { get; set; }
    public bool IsPassed { get; set; }

    // Navigation Properties
    public virtual User User { get; set; } = null!;
    public virtual Quiz Quiz { get; set; } = null!;
    public virtual ICollection<QuizResult> QuizResults { get; set; } = new List<QuizResult>();
}
