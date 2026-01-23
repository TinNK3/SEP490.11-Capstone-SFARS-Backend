using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class Quiz : BaseEntity
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public QuizDifficultyLevel DifficultyLevel { get; set; }
    public int PointsReward { get; set; }
    public bool IsActive { get; set; } = true;
}