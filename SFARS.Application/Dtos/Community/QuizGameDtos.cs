namespace SFARS.Application.Dtos.Community;

public class QuizListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string DifficultyLevel { get; set; } = null!;
    public int PointsReward { get; set; }
    public bool IsActive { get; set; }
}

public class QuizGameDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int PointsReward { get; set; }
    public List<QuestionGameDto> Questions { get; set; } = new();
}

public class QuestionGameDto
{
    public Guid Id { get; set; }
    public string Content { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public string QuestionType { get; set; } = null!;
    public List<OptionGameDto> Options { get; set; } = new();
}

public class OptionGameDto
{
    public Guid Id { get; set; }
    public string Content { get; set; } = null!;
}

public class QuizSubmitRequest
{
    public List<QuestionAnswerDto> Answers { get; set; } = new();
}

public class QuestionAnswerDto
{
    public Guid QuestionId { get; set; }
    public Guid? SelectedOptionId { get; set; }
    public List<Guid>? OrderedOptionIds { get; set; }
}

public class QuizResultDto
{
    public Guid HistoryId { get; set; }
    public int Score { get; set; }
    public int TotalQuestions { get; set; }
    public int PointsEarned { get; set; }
    public bool IsPassed { get; set; }
    public string Message { get; set; } = null!;
}

public class QuizManageDto
{
    public Guid? Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string DifficultyLevel { get; set; } = null!;
    public int PointsReward { get; set; }
    public bool IsActive { get; set; } = true;
    public List<QuestionManageDto> Questions { get; set; } = new();
}

public class QuestionManageDto
{
    public Guid? Id { get; set; }
    public string Content { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public string QuestionType { get; set; } = null!;
    public List<OptionManageDto> Options { get; set; } = new();
}

public class OptionManageDto
{
    public Guid? Id { get; set; }
    public string Content { get; set; } = null!;
    public bool IsCorrect { get; set; }
    public int? StepOrder { get; set; }
    public string? PenaltyNote { get; set; }
}

public class QuizHistoryDto
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = null!;
    public string? UserName { get; set; }
    public int Score { get; set; }
    public int TotalQuestions { get; set; }
    public int PointsEarned { get; set; }
    public bool IsPassed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class QuizHistoryDetailDto : QuizHistoryDto
{
    public List<QuizResultDetailDto> Details { get; set; } = new();
}

public class QuizResultDetailDto
{
    public Guid QuestionId { get; set; }
    public string QuestionContent { get; set; } = null!;
    public bool IsCorrect { get; set; }
    public Guid? SelectedOptionId { get; set; }
    public string? SelectedOptionContent { get; set; }
    public string? CorrectOptionContent { get; set; }
    public string? AnswerData { get; set; }
}
