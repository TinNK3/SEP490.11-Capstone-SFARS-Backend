using SFARS.Application.Dtos.Community;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Application.Interfaces.Services;

public interface IQuizService
{
    // User APIs
    Task<IServiceResult> GetQuizzesAsync();
    Task<IServiceResult> GetQuizForGameAsync(Guid quizId);
    Task<IServiceResult> SubmitQuizAsync(Guid userId, QuizSubmitRequest request);
    Task<IServiceResult> GetUserHistoryAsync(Guid userId);
    Task<IServiceResult> GetHistoryDetailAsync(Guid historyId, Guid? userId = null);

    // Admin APIs
    Task<IServiceResult> GetAllQuizzesAdminAsync();
    Task<IServiceResult> CreateQuizAsync(QuizManageDto dto);
    Task<IServiceResult> UpdateQuizAsync(Guid id, QuizManageDto dto);
    Task<IServiceResult> DeleteQuizAsync(Guid id);
    Task<IServiceResult> GetAllHistoryAdminAsync();
}
