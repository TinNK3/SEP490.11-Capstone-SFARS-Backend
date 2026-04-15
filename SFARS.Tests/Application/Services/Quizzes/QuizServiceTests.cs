using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Community;
using SFARS.Application.Interfaces.Services;
using SFARS.Application.Services;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Interfaces;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace SFARS.Tests.Application.Services.Quizzes;

public class QuizServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<ISystemMessageService> _msgServiceMock;
    private readonly Mock<ILogger<QuizService>> _loggerMock;
    private readonly Mock<IGenericRepository<Quiz, Guid>> _quizRepoMock;
    private readonly Mock<IGenericRepository<QuizQuestion, Guid>> _questionRepoMock;
    private readonly Mock<IGenericRepository<QuizOption, Guid>> _optionRepoMock;
    private readonly Mock<IGenericRepository<UserPoint, Guid>> _userPointRepoMock;
    private readonly Mock<IGenericRepository<QuizHistory, Guid>> _historyRepoMock;

    private readonly QuizService _sut;

    public QuizServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _mapperMock = new Mock<IMapper>();
        _msgServiceMock = new Mock<ISystemMessageService>();
        _loggerMock = new Mock<ILogger<QuizService>>();
        
        _uowMock.Setup(u => u.BeginTransactionAsync()).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.CommitTransactionAsync()).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.RollbackTransactionAsync()).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _msgServiceMock.Setup(m => m.GetMessageAsync(It.IsAny<string>())).ReturnsAsync("Test Message {0}");

        _quizRepoMock = new Mock<IGenericRepository<Quiz, Guid>>();
        _questionRepoMock = new Mock<IGenericRepository<QuizQuestion, Guid>>();
        _optionRepoMock = new Mock<IGenericRepository<QuizOption, Guid>>();
        _userPointRepoMock = new Mock<IGenericRepository<UserPoint, Guid>>();
        _historyRepoMock = new Mock<IGenericRepository<QuizHistory, Guid>>();

        _uowMock.Setup(u => u.Repository<Quiz, Guid>()).Returns(_quizRepoMock.Object);
        _uowMock.Setup(u => u.Repository<QuizQuestion, Guid>()).Returns(_questionRepoMock.Object);
        _uowMock.Setup(u => u.Repository<QuizOption, Guid>()).Returns(_optionRepoMock.Object);
        _uowMock.Setup(u => u.Repository<UserPoint, Guid>()).Returns(_userPointRepoMock.Object);
        _uowMock.Setup(u => u.Repository<QuizHistory, Guid>()).Returns(_historyRepoMock.Object);
        _uowMock.Setup(u => u.Repository<PointTransaction, Guid>()).Returns(new Mock<IGenericRepository<PointTransaction, Guid>>().Object);

        _sut = new QuizService(_msgServiceMock.Object, _uowMock.Object, _mapperMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetHistoryDetailAsync_Success_ReturnsHistoryWithDetails()
    {
        // Arrange
        var historyId = Guid.NewGuid();
        var quiz = new Quiz { Id = Guid.NewGuid(), Title = "History Quiz" };
        var question = new QuizQuestion { Id = Guid.NewGuid(), Content = "Q1" };
        question.Options.Add(new QuizOption { Id = Guid.NewGuid(), Content = "Correct Opt", IsCorrect = true, QuestionId = question.Id });
        
        var history = new QuizHistory
        {
            Id = historyId,
            Quiz = quiz,
            QuizResults = new List<QuizResult>
            {
                new QuizResult { QuestionId = question.Id, Question = question, IsCorrect = true }
            }
        };

        var histories = new List<QuizHistory> { history };
        
        _historyRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<QuizHistory>>(), true))
            .ReturnsAsync(histories);
        
        _mapperMock.Setup(m => m.Map<QuizHistoryDetailDto>(It.IsAny<QuizHistory>()))
            .Returns(new QuizHistoryDetailDto 
            { 
                Id = historyId, 
                Details = new List<QuizResultDetailDto>() 
            });

        // Act
        var result = await _sut.GetHistoryDetailAsync(historyId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var data = result.Data as QuizHistoryDetailDto;
        data.Should().NotBeNull();
        data!.Details.Should().HaveCount(1);
        data.Details[0].QuestionContent.Should().Be("Q1");
    }

    [Fact]
    public async Task CreateQuizAsync_Success_AddsToRepository()
    {
        // Arrange
        var dto = new QuizManageDto
        {
            Title = "New Admin Quiz",
            DifficultyLevel = "Easy",
            PointsReward = 50,
            Questions = new List<QuestionManageDto>
            {
                new QuestionManageDto 
                { 
                    Content = "Admin Q?", 
                    QuestionType = "MultipleChoice",
                    Options = new List<OptionManageDto> { new OptionManageDto { Content = "A1", IsCorrect = true } }
                }
            }
        };

        // Act
        var result = await _sut.CreateQuizAsync(dto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        _quizRepoMock.Verify(r => r.AddAsync(It.IsAny<Quiz>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task SubmitQuizAsync_Integration_RecordsHistory()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var qId = Guid.NewGuid();
        var optId = Guid.NewGuid();
        
        var question = new QuizQuestion { Id = qId, QuizId = Guid.NewGuid(), QuestionType = QuestionType.MultipleChoice };
        var options = new List<QuizOption> { new QuizOption { Id = optId, IsCorrect = true, QuestionId = qId } };
        
        _questionRepoMock.Setup(r => r.GetByIdAsync(qId)).ReturnsAsync(question);
        _optionRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<QuizOption>>(), true)).ReturnsAsync(options);
        
        var request = new QuizSubmitRequest 
        { 
            Answers = new List<QuestionAnswerDto> { new QuestionAnswerDto { QuestionId = qId, SelectedOptionId = optId } } 
        };

        // Act
        await _sut.SubmitQuizAsync(userId, request);

        // Assert
        _historyRepoMock.Verify(r => r.AddAsync(It.Is<QuizHistory>(h => h.UserId == userId && h.Score == 1)), Times.Once);
    }
}
