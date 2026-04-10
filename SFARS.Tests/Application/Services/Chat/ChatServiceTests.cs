using FluentAssertions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Chat;
using SFARS.Application.Services;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Interfaces;
using SFARS.Domain.Specifications.Params;
using SFARS.Application.Dtos;
using System.Collections;
using System.Linq.Expressions;
using ChatHistoryItem = SFARS.Domain.Interfaces.Infrastructure.ChatHistoryItem;

namespace SFARS.Tests.Application.Services.Chat;

/// <summary>
/// Unit tests for ChatService:
/// - SendMessageAsync    (POST /api/chat/send)
/// - GetSessionsAsync    (GET  /api/chat/sessions)
/// - GetMessagesAsync    (GET  /api/chat/sessions/{id}/messages)
///
/// Scope: Application service layer only — no EF Core, no real DB.
/// ChatService handles RAG-based AI chat with context from DB.
/// </summary>
public class ChatServiceTests
{
    private readonly Mock<ISystemMessageService> _msgServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IGeminiAiService> _geminiServiceMock;
    private readonly Mock<ILogger<ChatService>> _loggerMock;
    private readonly Mock<IGenericRepository<ChatSession, Guid>> _sessionRepoMock;
    private readonly Mock<IGenericRepository<ChatMessage, Guid>> _messageRepoMock;
    private readonly Mock<IGenericRepository<Snake, Guid>> _snakeRepoMock;
    private readonly Mock<IGenericRepository<FirstAidDetail, Guid>> _firstAidRepoMock;

    private readonly ChatService _sut;

    public ChatServiceTests()
    {
        _msgServiceMock = new Mock<ISystemMessageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _geminiServiceMock = new Mock<IGeminiAiService>();
        _loggerMock = new Mock<ILogger<ChatService>>();
        _sessionRepoMock = new Mock<IGenericRepository<ChatSession, Guid>>();
        _messageRepoMock = new Mock<IGenericRepository<ChatMessage, Guid>>();
        _snakeRepoMock = new Mock<IGenericRepository<Snake, Guid>>();
        _firstAidRepoMock = new Mock<IGenericRepository<FirstAidDetail, Guid>>();

        _unitOfWorkMock.Setup(x => x.Repository<ChatSession, Guid>()).Returns(_sessionRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<ChatMessage, Guid>()).Returns(_messageRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<Snake, Guid>()).Returns(_snakeRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<FirstAidDetail, Guid>()).Returns(_firstAidRepoMock.Object);

        _msgServiceMock
            .Setup(x => x.GetMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((string code) => $"Message for {code}");

        _geminiServiceMock.Setup(x => x.ModelName).Returns("gemini-1.5-flash");

        // Default mock: GetQueryable returns empty async-enumerable collections
        SetupDefaultQueryableMocks();

        var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());

        _sut = new ChatService(
            _msgServiceMock.Object,
            _unitOfWorkMock.Object,
            _geminiServiceMock.Object,
            _loggerMock.Object,
            memoryCache
        );
    }

    /// <summary>
    /// Sets up default empty queryable mocks for all repos used by ChatService.
    /// Individual tests can override specific setups as needed.
    /// </summary>
    private void SetupDefaultQueryableMocks()
    {
        _sessionRepoMock.Setup(r => r.GetQueryable(It.IsAny<bool>()))
            .Returns(ToAsyncQueryable(new List<ChatSession>()));
        _messageRepoMock.Setup(r => r.GetQueryable(It.IsAny<bool>()))
            .Returns(ToAsyncQueryable(new List<ChatMessage>()));
        _snakeRepoMock.Setup(r => r.GetQueryable(It.IsAny<bool>()))
            .Returns(ToAsyncQueryable(new List<Snake>()));
        _firstAidRepoMock.Setup(r => r.GetQueryable(It.IsAny<bool>()))
            .Returns(ToAsyncQueryable(new List<FirstAidDetail>()));
    }

    #region SendMessageAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SendMessageAsync with empty user ID
    /// Precondition: userId is Guid.Empty
    /// Expected Result: Returns Auth_Warning0013 (invalid user)
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_EmptyUserId_ReturnsAuthWarning()
    {
        // Arrange
        var userId = Guid.Empty;
        var sessionId = Guid.NewGuid();
        var message = "Hello, AI!";

        // Act
        var result = await _sut.SendMessageAsync(userId, sessionId, message);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0013);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SendMessageAsync with empty/whitespace message
    /// Precondition: message is null, empty, or whitespace
    /// Expected Result: Returns SYS_Warning0001 (invalid input)
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task SendMessageAsync_EmptyMessage_ReturnsValidationWarning(string? message)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var result = await _sut.SendMessageAsync(userId, sessionId, message!);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SendMessageAsync with non-existent session ID
    /// Precondition: Valid userId but sessionId does not exist in DB
    /// Expected Result: Returns SYS_Warning0002 (session not found)
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_SessionNotFound_ReturnsNotFoundWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var message = "Hello!";

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync((ChatSession?)null);

        // Act
        var result = await _sut.SendMessageAsync(userId, sessionId, message);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SendMessageAsync when user tries to access another user's session
    /// Precondition: Session exists but belongs to different user
    /// Expected Result: Returns SYS_Warning0002 (unauthorized access)
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_WrongUserSession_ReturnsUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var message = "Hello!";

        var session = new ChatSession
        {
            Id = sessionId,
            UserId = otherUserId, // Different user!
            IsActive = true
        };

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(session);

        // Act
        var result = await _sut.SendMessageAsync(userId, sessionId, message);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SendMessageAsync on inactive session
    /// Precondition: Session exists and belongs to user but IsActive = false
    /// Expected Result: Returns Chat_Warning0001 (session inactive)
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_InactiveSession_ReturnsInactiveWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var message = "Hello!";

        var session = new ChatSession
        {
            Id = sessionId,
            UserId = userId,
            IsActive = false // Inactive!
        };

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(session);

        // Act
        var result = await _sut.SendMessageAsync(userId, sessionId, message);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Chat_Warning0001);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SendMessageAsync creates new session when sessionId is null
    /// Precondition: Valid userId, no sessionId, valid message
    /// Expected Result: Returns Chat_Success0001 with new session created
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_NullSessionId_CreatesNewSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        Guid? sessionId = null;
        var message = "What should I do if bitten by a cobra?";

        // Embedding call will throw → triggers keyword fallback
        _geminiServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Embedding unavailable"));

        _geminiServiceMock
            .Setup(x => x.ChatWithContextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatHistoryItem>?>()))
            .ReturnsAsync("If bitten by a cobra, seek immediate medical attention...");

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(3); // Session + 2 messages

        // Act
        var result = await _sut.SendMessageAsync(userId, sessionId, message);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Chat_Success0001);
        result.Data.Should().NotBeNull();
        var response = result.Data as SendMessageResponseDto;
        response.Should().NotBeNull();
        response!.SessionId.Should().NotBeEmpty();
        response.UserMessage.Should().NotBeNull();
        response.AiMessage.Should().NotBeNull();

        _sessionRepoMock.Verify(r => r.AddAsync(It.Is<ChatSession>(s => s.UserId == userId)), Times.Once);
        _messageRepoMock.Verify(r => r.AddAsync(It.IsAny<ChatMessage>()), Times.Exactly(2));
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: SendMessageAsync sends message to existing session
    /// Precondition: Valid userId, existing valid sessionId, valid message
    /// Expected Result: Returns Chat_Success0001 with messages saved
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_ExistingSession_SendsMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var message = "Tell me about king cobra";

        var session = new ChatSession
        {
            Id = sessionId,
            UserId = userId,
            Title = "Chat about snakes",
            IsActive = true,
            LastMessageAt = DateTime.UtcNow.AddMinutes(-10)
        };

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync(session);

        _geminiServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Embedding unavailable"));

        _geminiServiceMock
            .Setup(x => x.ChatWithContextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatHistoryItem>?>()))
            .ReturnsAsync("King cobra is the world's longest venomous snake...");

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(2); // 2 messages

        // Act
        var result = await _sut.SendMessageAsync(userId, sessionId, message);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Chat_Success0001);
        result.Data.Should().NotBeNull();
        var response = result.Data as SendMessageResponseDto;
        response.Should().NotBeNull();
        response!.SessionId.Should().Be(sessionId);

        _messageRepoMock.Verify(r => r.AddAsync(It.Is<ChatMessage>(m => 
            m.ChatSessionId == sessionId && m.SenderType == ChatSenderType.User)), Times.Once);
        _messageRepoMock.Verify(r => r.AddAsync(It.Is<ChatMessage>(m => 
            m.ChatSessionId == sessionId && m.SenderType == ChatSenderType.AI)), Times.Once);
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: SendMessageAsync with maximum length title generation
    /// Precondition: First message exceeds 50 characters (MaxTitleLength)
    /// Expected Result: Title is truncated to 50 chars + "..."
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_LongFirstMessage_TruncatesTitle()
    {
        // Arrange
        var userId = Guid.NewGuid();
        Guid? sessionId = null;
        var longMessage = new string('a', 100); // 100 characters

        _geminiServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Embedding unavailable"));

        _geminiServiceMock
            .Setup(x => x.ChatWithContextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatHistoryItem>?>()))
            .ReturnsAsync("Response");

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(3);

        // Act
        var result = await _sut.SendMessageAsync(userId, sessionId, longMessage);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Chat_Success0001);

        _sessionRepoMock.Verify(r => r.AddAsync(It.Is<ChatSession>(s => 
            s.Title != null && s.Title.Length == 53 && s.Title.EndsWith("..."))), Times.Once);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SendMessageAsync when Gemini service throws exception
    /// Precondition: Valid inputs but Gemini API fails
    /// Expected Result: Returns fallback message Chat_Fail0001
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_GeminiServiceFails_ReturnsFallbackMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var message = "Hello!";

        var session = new ChatSession
        {
            Id = sessionId,
            UserId = userId,
            IsActive = true
        };

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync(session);

        _geminiServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Embedding unavailable"));

        _geminiServiceMock
            .Setup(x => x.ChatWithContextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatHistoryItem>?>()))
            .ThrowsAsync(new Exception("Gemini API error"));

        _msgServiceMock
            .Setup(x => x.GetMessageAsync(ResultCodeConst.Chat_Fail0001))
            .ReturnsAsync("AI service temporarily unavailable");

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        var result = await _sut.SendMessageAsync(userId, sessionId, message);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Chat_Success0001);
        var response = result.Data as SendMessageResponseDto;
        response!.AiMessage.Content.Should().Contain("AI service temporarily unavailable");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Gemini chat failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: SendMessageAsync when database save fails
    /// Precondition: All processing succeeds but SaveChangesAsync returns 0
    /// Expected Result: Returns SYS_Fail0001 (database save failed)
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_SaveChangesFails_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var message = "Hello!";

        var session = new ChatSession
        {
            Id = sessionId,
            UserId = userId,
            IsActive = true
        };

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync(session);

        _geminiServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Embedding unavailable"));

        _geminiServiceMock
            .Setup(x => x.ChatWithContextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatHistoryItem>?>()))
            .ReturnsAsync("Response");

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(0); // Save failed!

        // Act
        var result = await _sut.SendMessageAsync(userId, sessionId, message);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Fail0001);
        result.Data.Should().BeNull();
    }

    #endregion

    #region GetSessionsAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: GetSessionsAsync with empty user ID
    /// Precondition: userId is Guid.Empty
    /// Expected Result: Returns Auth_Warning0013
    /// </summary>
    [Fact]
    public async Task GetSessionsAsync_EmptyUserId_ReturnsAuthWarning()
    {
        // Arrange
        var userId = Guid.Empty;

        // Act
        var result = await _sut.GetSessionsAsync(userId, new BaseSpecParams { Page = 1, PageSize = 10 });

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0013);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: GetSessionsAsync returns paginated sessions for user
    /// Precondition: User has multiple sessions
    /// Expected Result: Returns sessions ordered by LastMessageAt descending
    /// </summary>
    [Fact]
    public async Task GetSessionsAsync_ValidUser_ReturnsSessions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var sessions = new List<ChatSession>
        {
            new ChatSession { Id = Guid.NewGuid(), UserId = userId, Title = "Session 1", LastMessageAt = DateTime.UtcNow.AddHours(-1), IsActive = true, CreatedAt = DateTime.UtcNow.AddHours(-2) },
            new ChatSession { Id = Guid.NewGuid(), UserId = userId, Title = "Session 2", LastMessageAt = DateTime.UtcNow, IsActive = true, CreatedAt = DateTime.UtcNow },
            new ChatSession { Id = Guid.NewGuid(), UserId = otherUserId, Title = "Other Session", LastMessageAt = DateTime.UtcNow, IsActive = true, CreatedAt = DateTime.UtcNow }
        };

        _sessionRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<ChatSession>>(), It.IsAny<bool>()))
            .ReturnsAsync(sessions.Where(s => s.UserId == userId).OrderByDescending(s => s.LastMessageAt).ToList());
        _sessionRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<ChatSession>>()))
            .ReturnsAsync(2);

        // Act
        var result = await _sut.GetSessionsAsync(userId, new BaseSpecParams { Page = 1, PageSize = 10 });

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var data = result.Data as PaginatedResultDto<ChatSessionDto>;
        data.Should().NotBeNull();
        data!.Items.Should().NotBeNull();
        data.Items.Count().Should().Be(2); // Only user's sessions
        data.Items.First().Title.Should().Be("Session 2"); // Most recent first
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: GetSessionsAsync with pagination
    /// Precondition: User has 15 sessions, requesting page 2 with page size 10
    /// Expected Result: Returns last 5 sessions
    /// </summary>
    [Fact]
    public async Task GetSessionsAsync_Pagination_ReturnsCorrectPage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessions = Enumerable.Range(1, 15)
            .Select(i => new ChatSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = $"Session {i}",
                LastMessageAt = DateTime.UtcNow.AddHours(-i),
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddHours(-i)
            })
            .ToList();

        _sessionRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<ChatSession>>(), It.IsAny<bool>()))
            .ReturnsAsync(sessions.Skip(10).Take(10).ToList()); // page 2, page size 10
        _sessionRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<ChatSession>>()))
            .ReturnsAsync(15);

        // Act
        var result = await _sut.GetSessionsAsync(userId, new BaseSpecParams { Page = 2, PageSize = 10 });

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var data = result.Data as PaginatedResultDto<ChatSessionDto>;
        data.Should().NotBeNull();
        data!.Items.Count().Should().Be(5); // Remaining sessions
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: GetSessionsAsync when user has no sessions
    /// Precondition: User exists but has no chat sessions
    /// Expected Result: Returns empty list with success
    /// </summary>
    [Fact]
    public async Task GetSessionsAsync_NoSessions_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Default setup already returns empty list for GetAllWithSpecAsync / CountAsync due to Moq behavior returning empty list/0, but we can explicit:
        _sessionRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<ChatSession>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<ChatSession>());
        _sessionRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<ChatSession>>()))
            .ReturnsAsync(0);

        // Act
        var result = await _sut.GetSessionsAsync(userId, new BaseSpecParams { Page = 1, PageSize = 10 });

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var data = result.Data as PaginatedResultDto<ChatSessionDto>;
        data.Should().NotBeNull();
        data!.Items.Should().BeEmpty();
    }

    #endregion

    #region GetMessagesAsync Tests

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: GetMessagesAsync with non-existent session
    /// Precondition: sessionId does not exist in database
    /// Expected Result: Returns SYS_Warning0002
    /// </summary>
    [Fact]
    public async Task GetMessagesAsync_SessionNotFound_ReturnsWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync((ChatSession?)null);

        // Act
        var result = await _sut.GetMessagesAsync(userId, sessionId, new BaseSpecParams { Page = 1, PageSize = 10 });

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: ABNORMAL
    /// Tests: GetMessagesAsync when session belongs to different user
    /// Precondition: Session exists but userId does not match
    /// Expected Result: Returns SYS_Warning0002 (unauthorized)
    /// </summary>
    [Fact]
    public async Task GetMessagesAsync_WrongUser_ReturnsUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var session = new ChatSession
        {
            Id = sessionId,
            UserId = otherUserId // Different user
        };

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync(session);

        // Act
        var result = await _sut.GetMessagesAsync(userId, sessionId, new BaseSpecParams { Page = 1, PageSize = 10 });

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0002);
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// Test Type: NORMAL
    /// Tests: GetMessagesAsync returns paginated messages for session
    /// Precondition: Valid session with messages
    /// Expected Result: Returns messages ordered by CreatedAt ascending
    /// </summary>
    [Fact]
    public async Task GetMessagesAsync_ValidSession_ReturnsMessages()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var session = new ChatSession { Id = sessionId, UserId = userId };

        var messages = new List<ChatMessage>
        {
            new ChatMessage { Id = Guid.NewGuid(), ChatSessionId = sessionId, SenderType = ChatSenderType.User, Content = "Hello", CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new ChatMessage { Id = Guid.NewGuid(), ChatSessionId = sessionId, SenderType = ChatSenderType.AI, Content = "Hi there", CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new ChatMessage { Id = Guid.NewGuid(), ChatSessionId = Guid.NewGuid(), SenderType = ChatSenderType.User, Content = "Other session", CreatedAt = DateTime.UtcNow }
        };

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync(session);
        _messageRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<ChatMessage>>(), It.IsAny<bool>()))
            .ReturnsAsync(messages.Where(m => m.ChatSessionId == sessionId).OrderBy(m => m.CreatedAt).ToList());
        _messageRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<ChatMessage>>()))
            .ReturnsAsync(2);

        // Act
        var result = await _sut.GetMessagesAsync(userId, sessionId, new BaseSpecParams { Page = 1, PageSize = 10 });

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var data = result.Data as PaginatedResultDto<ChatMessageDto>;
        data.Should().NotBeNull();
        data!.Items.Count().Should().Be(2); // Only messages from this session
        data.Items.First().Content.Should().Be("Hello"); // Oldest first
    }

    /// <summary>
    /// Test Type: BOUNDARY
    /// Tests: GetMessagesAsync with pagination
    /// Precondition: Session has 25 messages, requesting page 3 with page size 10
    /// Expected Result: Returns last 5 messages
    /// </summary>
    [Fact]
    public async Task GetMessagesAsync_Pagination_ReturnsCorrectPage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var session = new ChatSession { Id = sessionId, UserId = userId };

        var messages = Enumerable.Range(1, 25)
            .Select(i => new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatSessionId = sessionId,
                SenderType = i % 2 == 0 ? ChatSenderType.AI : ChatSenderType.User,
                Content = $"Message {i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-25 + i)
            })
            .ToList();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync(session);
        _messageRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<ChatMessage>>(), It.IsAny<bool>()))
            .ReturnsAsync(messages.Skip(20).Take(10).ToList()); // Page 3, pageSize 10
        _messageRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<ChatMessage>>()))
            .ReturnsAsync(25);

        // Act
        var result = await _sut.GetMessagesAsync(userId, sessionId, new BaseSpecParams { Page = 3, PageSize = 10 });

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var data = result.Data as PaginatedResultDto<ChatMessageDto>;
        data.Should().NotBeNull();
        data!.Items.Count().Should().Be(5); // Last 5 messages
    }

    #endregion

    #region Async Queryable Test Helpers

    private static IQueryable<T> ToAsyncQueryable<T>(IEnumerable<T> source)
    {
        return new TestAsyncEnumerable<T>(source);
    }

    private sealed class TestAsyncQueryProvider<TEntity>(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression)
        {
            return new TestAsyncEnumerable<TEntity>(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new TestAsyncEnumerable<TElement>(expression);
        }

        public object? Execute(Expression expression)
        {
            return inner.Execute(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return inner.Execute<TResult>(expression);
        }

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var expectedResultType = typeof(TResult).GetGenericArguments().First();
            var executionResult = typeof(IQueryProvider)
                .GetMethods()
                .First(m => m.Name == nameof(IQueryProvider.Execute) && m.IsGenericMethod)
                .MakeGenericMethod(expectedResultType)
                .Invoke(inner, new[] { expression });

            return (TResult)typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(expectedResultType)
                .Invoke(null, new[] { executionResult })!;
        }
    }

    private sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public TestAsyncEnumerable(Expression expression) : base(expression) { }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
        }

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }

    private sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;

        public ValueTask<bool> MoveNextAsync()
        {
            return new ValueTask<bool>(inner.MoveNext());
        }

        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
