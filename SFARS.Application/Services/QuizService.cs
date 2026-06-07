using MapsterMapper;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Community;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Application.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;
using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Interfaces.Services;
using System.Text.Json;

namespace SFARS.Application.Services;

public class QuizService : ReadOnlyService<Quiz, QuizListItemDto, Guid>, IQuizService
{
    public QuizService(
        ISystemMessageService msgService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<QuizService> logger) : base(msgService, unitOfWork, mapper, logger)
    {
    }

    public async Task<IServiceResult> GetQuizzesAsync()
    {
        try
        {
            var spec = new QuizSpecification(activeOnly: true);
            var quizzes = await _unitOfWork.Repository<Quiz, Guid>().GetAllWithSpecAsync(spec);
            var dtos = _mapper.Map<List<QuizListItemDto>>(quizzes);

            return new ServiceResult(ResultCodeConst.SYS_Success0002, string.Empty, dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quizzes");
            return new ServiceResult(ResultCodeConst.SYS_Fail0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }
    }

    public async Task<IServiceResult> GetQuizForGameAsync(Guid quizId)
    {
        try
        {
            var spec = QuizSpecification.WithDetails(quizId);
            var quiz = (await _unitOfWork.Repository<Quiz, Guid>().GetAllWithSpecAsync(spec)).FirstOrDefault();

            if (quiz == null)
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0004, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
            }

            var dto = _mapper.Map<QuizGameDto>(quiz);

            // Shuffle options for security within each question
            var rng = new Random();
            foreach (var q in dto.Questions)
            {
                q.Options = q.Options.OrderBy(_ => rng.Next()).ToList();
            }

            return new ServiceResult(ResultCodeConst.SYS_Success0002, string.Empty, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quiz for game {QuizId}", quizId);
            return new ServiceResult(ResultCodeConst.SYS_Fail0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }
    }

    public async Task<IServiceResult> SubmitQuizAsync(Guid userId, QuizSubmitRequest request)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync();

            int correctCount = 0;
            var results = new List<QuizResult>();
            Guid quizId = Guid.Empty;

            foreach (var answer in request.Answers)
            {
                var question = await _unitOfWork.Repository<QuizQuestion, Guid>().GetByIdAsync(answer.QuestionId);
                if (question == null) continue;

                if (quizId == Guid.Empty) quizId = question.QuizId;

                var options = await _unitOfWork.Repository<QuizOption, Guid>()
                    .GetAllWithSpecAsync(new BaseSpecification<QuizOption>(o => o.QuestionId == answer.QuestionId));

                bool isCorrect = false;
                string? answerData = null;

                if (question.QuestionType == QuestionType.Ordering)
                {
                    var correctOrder = options.OrderBy(o => o.StepOrder).Select(o => o.Id).ToList();
                    isCorrect = answer.OrderedOptionIds != null && answer.OrderedOptionIds.SequenceEqual(correctOrder);
                    answerData = answer.OrderedOptionIds != null ? JsonSerializer.Serialize(answer.OrderedOptionIds) : null;
                }
                else
                {
                    isCorrect = options.Any(o => o.Id == answer.SelectedOptionId && o.IsCorrect);
                }

                if (isCorrect) correctCount++;

                results.Add(new QuizResult
                {
                    Id = Guid.NewGuid(),
                    QuestionId = answer.QuestionId,
                    IsCorrect = isCorrect,
                    SelectedOptionId = answer.SelectedOptionId,
                    AnswerData = answerData,
                    CreatedAt = DateTime.UtcNow
                });
            }

            int totalReward = 0;
            int totalQuestions = 0;
            if (quizId != Guid.Empty)
            {
                var quiz = await _unitOfWork.Repository<Quiz, Guid>().GetByIdAsync(quizId);
                if (quiz != null)
                {
                    totalQuestions = quiz.QuizQuestions.Count;
                    if (correctCount == totalQuestions && totalQuestions > 0)
                    {
                        totalReward = quiz.PointsReward;
                    }
                }
            }

            var history = new QuizHistory
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                QuizId = quizId,
                Score = correctCount,
                TotalQuestions = totalQuestions,
                PointsEarned = totalReward,
                IsPassed = totalReward > 0,
                CreatedAt = DateTime.UtcNow,
                QuizResults = results
            };

            await _unitOfWork.Repository<QuizHistory, Guid>().AddAsync(history);

            if (totalReward > 0)
            {
                var transaction = new PointTransaction
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Amount = totalReward,
                    ActivityType = PointActivityType.Earn,
                    ReferenceId = history.Id,
                    Description = $"Hoàn thành Quiz thành công. ({correctCount}/{totalQuestions} câu đúng)",
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Repository<PointTransaction, Guid>().AddAsync(transaction);

                var upEntity = await _unitOfWork.Repository<UserPoint, Guid>()
                    .GetWithSpecAsync(new BaseSpecification<UserPoint>(up => up.UserId == userId));
                if (upEntity != null)
                {
                    upEntity.CurrentPoints += totalReward;
                    upEntity.LifetimePoints += totalReward;
                    upEntity.UpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.Repository<UserPoint, Guid>().UpdateAsync(upEntity);
                }
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            var resultDto = new QuizResultDto
            {
                HistoryId = history.Id,
                Score = correctCount,
                TotalQuestions = totalQuestions,
                PointsEarned = totalReward,
                IsPassed = totalReward > 0,
                Message = totalReward > 0
                    ? string.Format(await _msgService.GetMessageAsync(ResultCodeConst.Comm_Success0002), totalReward)
                    : "Bạn chưa đạt yêu cầu để nhận thưởng. Hãy cố gắng lần sau!"
            };

            return new ServiceResult(ResultCodeConst.SYS_Success0001, resultDto.Message, resultDto);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error submitting quiz for user {UserId}", userId);
            return new ServiceResult(ResultCodeConst.SYS_Fail0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }
    }

    public async Task<IServiceResult> GetUserHistoryAsync(Guid userId)
    {
        try
        {
            var spec = new BaseSpecification<QuizHistory>(h => h.UserId == userId);
            spec.ApplyInclude(q => q.Include(h => h.Quiz));
            spec.AddOrderByDescending(h => h.CreatedAt);

            var histories = await _unitOfWork.Repository<QuizHistory, Guid>().GetAllWithSpecAsync(spec);
            var dtos = _mapper.Map<List<QuizHistoryDto>>(histories);

            return new ServiceResult(ResultCodeConst.SYS_Success0002, string.Empty, dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user quiz history {UserId}", userId);
            return new ServiceResult(ResultCodeConst.SYS_Fail0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }
    }

    public async Task<IServiceResult> GetHistoryDetailAsync(Guid historyId, Guid? userId = null)
    {
        try
        {
            var spec = new BaseSpecification<QuizHistory>(h => h.Id == historyId);
            if (userId.HasValue)
            {
                spec.AddFilter(h => h.UserId == userId.Value);
            }
            
            spec.ApplyInclude(q => q.Include(h => h.Quiz));
            spec.ApplyInclude(q => q.Include(h => h.User));
            spec.ApplyInclude(q => q.Include(h => h.QuizResults)
                                    .ThenInclude(r => r.Question)
                                        .ThenInclude(q => q.Options));

            var history = (await _unitOfWork.Repository<QuizHistory, Guid>().GetAllWithSpecAsync(spec)).FirstOrDefault();

            if (history == null) return new ServiceResult(ResultCodeConst.SYS_Warning0004, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));

            var dto = _mapper.Map<QuizHistoryDetailDto>(history);

            foreach (var res in history.QuizResults)
            {
                var detailDto = new QuizResultDetailDto
                {
                    QuestionId = res.QuestionId,
                    QuestionContent = res.Question.Content,
                    IsCorrect = res.IsCorrect,
                    SelectedOptionId = res.SelectedOptionId,
                    AnswerData = res.AnswerData
                };

                var selectedOpt = res.Question.Options.FirstOrDefault(o => o.Id == res.SelectedOptionId);
                detailDto.SelectedOptionContent = selectedOpt?.Content;

                if (res.Question.QuestionType == QuestionType.Ordering)
                {
                    var correctSequence = res.Question.Options.OrderBy(o => o.StepOrder).Select(o => o.Content).ToList();
                    detailDto.CorrectOptionContent = string.Join(" -> ", correctSequence);
                }
                else
                {
                    var correctOpt = res.Question.Options.FirstOrDefault(o => o.IsCorrect);
                    detailDto.CorrectOptionContent = correctOpt?.Content;
                }

                dto.Details.Add(detailDto);
            }

            return new ServiceResult(ResultCodeConst.SYS_Success0002, string.Empty, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history details {HistoryId}", historyId);
            return new ServiceResult(ResultCodeConst.SYS_Fail0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }
    }

    // Admin APIs
    public async Task<IServiceResult> GetAllQuizzesAdminAsync()
    {
        try
        {
            var quizzes = await _unitOfWork.Repository<Quiz, Guid>().GetAllAsync();
            var dtos = _mapper.Map<List<QuizListItemDto>>(quizzes);
            return new ServiceResult(ResultCodeConst.SYS_Success0002, string.Empty, dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all quizzes for admin");
            return new ServiceResult(ResultCodeConst.SYS_Fail0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }
    }

    public async Task<IServiceResult> CreateQuizAsync(QuizManageDto dto)
    {
        try
        {
            var quiz = new Quiz
            {
                Id = Guid.NewGuid(),
                Title = dto.Title,
                Description = dto.Description,
                DifficultyLevel = Enum.Parse<QuizDifficultyLevel>(dto.DifficultyLevel),
                PointsReward = dto.PointsReward,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var qDto in dto.Questions)
            {
                var question = new QuizQuestion
                {
                    Id = Guid.NewGuid(),
                    Content = qDto.Content,
                    ImageUrl = qDto.ImageUrl,
                    QuestionType = Enum.Parse<QuestionType>(qDto.QuestionType),
                    CreatedAt = DateTime.UtcNow
                };

                foreach (var oDto in qDto.Options)
                {
                    question.Options.Add(new QuizOption
                    {
                        Id = Guid.NewGuid(),
                        Content = oDto.Content,
                        IsCorrect = oDto.IsCorrect,
                        StepOrder = oDto.StepOrder,
                        PenaltyNote = oDto.PenaltyNote,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                quiz.QuizQuestions.Add(question);
            }

            await _unitOfWork.Repository<Quiz, Guid>().AddAsync(quiz);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult(ResultCodeConst.SYS_Success0001, "Created quiz successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating quiz");
            return new ServiceResult(ResultCodeConst.SYS_Fail0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }
    }

    public async Task<IServiceResult> UpdateQuizAsync(Guid id, QuizManageDto dto)
    {
        try
        {
            var spec = QuizSpecification.WithDetails(id);
            var quiz = (await _unitOfWork.Repository<Quiz, Guid>().GetAllWithSpecAsync(spec)).FirstOrDefault();

            if (quiz == null) return new ServiceResult(ResultCodeConst.SYS_Warning0004, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));

            quiz.Title = dto.Title;
            quiz.Description = dto.Description;
            quiz.DifficultyLevel = Enum.Parse<QuizDifficultyLevel>(dto.DifficultyLevel);
            quiz.PointsReward = dto.PointsReward;
            quiz.IsActive = dto.IsActive;
            quiz.UpdatedAt = DateTime.UtcNow;

            // Delete existing questions
            await _unitOfWork.Repository<QuizQuestion, Guid>().DeleteRangeAsync(quiz.QuizQuestions.Select(q => q.Id).ToArray());
            quiz.QuizQuestions.Clear();

            foreach (var qDto in dto.Questions)
            {
                var question = new QuizQuestion
                {
                    Id = Guid.NewGuid(),
                    Content = qDto.Content,
                    ImageUrl = qDto.ImageUrl,
                    QuestionType = Enum.Parse<QuestionType>(qDto.QuestionType),
                    CreatedAt = DateTime.UtcNow
                };

                foreach (var oDto in qDto.Options)
                {
                    question.Options.Add(new QuizOption
                    {
                        Id = Guid.NewGuid(),
                        Content = oDto.Content,
                        IsCorrect = oDto.IsCorrect,
                        StepOrder = oDto.StepOrder,
                        PenaltyNote = oDto.PenaltyNote,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                quiz.QuizQuestions.Add(question);
            }

            await _unitOfWork.Repository<Quiz, Guid>().UpdateAsync(quiz);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult(ResultCodeConst.SYS_Success0003, "Updated quiz successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating quiz {QuizId}", id);
            return new ServiceResult(ResultCodeConst.SYS_Fail0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }
    }

    public async Task<IServiceResult> DeleteQuizAsync(Guid id)
    {
        try
        {
            await _unitOfWork.Repository<Quiz, Guid>().DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();
            return new ServiceResult(ResultCodeConst.SYS_Success0004, "Deleted quiz successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting quiz {QuizId}", id);
            return new ServiceResult(ResultCodeConst.SYS_Fail0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }
    }

    public async Task<IServiceResult> GetAllHistoryAdminAsync()
    {
        try
        {
            var spec = new BaseSpecification<QuizHistory>(_ => true);
            spec.ApplyInclude(q => q.Include(h => h.Quiz));
            spec.ApplyInclude(q => q.Include(h => h.User));
            spec.AddOrderByDescending(h => h.CreatedAt);

            var histories = await _unitOfWork.Repository<QuizHistory, Guid>().GetAllWithSpecAsync(spec);
            var dtos = _mapper.Map<List<QuizHistoryDto>>(histories);

            return new ServiceResult(ResultCodeConst.SYS_Success0002, string.Empty, dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all quiz history for admin");
            return new ServiceResult(ResultCodeConst.SYS_Fail0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }
    }
}
