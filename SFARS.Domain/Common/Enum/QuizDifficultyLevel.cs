using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum QuizDifficultyLevel
    {
        [Description("Dễ")]
        Easy,
        [Description("Trung bình")]
        Medium,
        [Description("Khó")]
        Hard
    }
}