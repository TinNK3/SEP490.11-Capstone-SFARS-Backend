using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Chat;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using System.Text;

namespace SFARS.Application.Services;

/// <summary>
/// General AI chatbox service — RAG-based (DB context → Gemini → post-check).
/// Not tied to any specific incident.
/// </summary>
public class ChatService : IChatService
{
    private readonly ISystemMessageService _msgService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGeminiAiService _geminiService;
    private readonly ILogger<ChatService> _logger;

    /// <summary>
    /// Max recent messages to include as conversation history for Gemini multi-turn.
    /// </summary>
    private const int MaxHistoryMessages = 10;

    /// <summary>
    /// Max characters for auto-generated session title from first user message.
    /// </summary>
    private const int MaxTitleLength = 50;

    public ChatService(
        ISystemMessageService msgService,
        IUnitOfWork unitOfWork,
        IGeminiAiService geminiService,
        ILogger<ChatService> logger)
    {
        _msgService = msgService;
        _unitOfWork = unitOfWork;
        _geminiService = geminiService;
        _logger = logger;
    }

    #region Public Methods

    public async Task<IServiceResult> SendMessageAsync(Guid userId, Guid? sessionId, string message)
    {
        if (userId == Guid.Empty)
        {
            return new ServiceResult(
                ResultCodeConst.Auth_Warning0013,
                await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));
        }

        var now = DateTime.UtcNow;
        var trimmedMessage = message.Trim();

        // Resolve or create session
        ChatSession session;
        if (sessionId.HasValue && sessionId.Value != Guid.Empty)
        {
            var existing = await _unitOfWork.Repository<ChatSession, Guid>().GetByIdAsync(sessionId.Value);
            if (existing == null || existing.UserId != userId)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0002,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));
            }

            if (!existing.IsActive)
            {
                return new ServiceResult(
                    ResultCodeConst.Chat_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.Chat_Warning0001));
            }

            session = existing;
        }
        else
        {
            // Auto-create new session
            session = new ChatSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = trimmedMessage.Length > MaxTitleLength
                    ? trimmedMessage[..MaxTitleLength] + "..."
                    : trimmedMessage,
                LastMessageAt = now,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };
            await _unitOfWork.Repository<ChatSession, Guid>().AddAsync(session);
        }

        // Save user message
        var userMsg = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = session.Id,
            SenderType = ChatSenderType.User,
            Content = trimmedMessage,
            CreatedAt = now,
            CreatedBy = userId
        };
        await _unitOfWork.Repository<ChatMessage, Guid>().AddAsync(userMsg);

        // Build RAG context from DB using retrieval logic based on user message
        var contextPacket = await BuildContextPacketAsync(trimmedMessage);

        // Load conversation history
        var history = await GetConversationHistoryAsync(session.Id);

        // Call Gemini with RAG
        string aiResponseText;
        try
        {
            aiResponseText = await _geminiService.ChatWithContextAsync(
                systemPrompt: BuildSystemPrompt(),
                contextData: contextPacket,
                userMessage: trimmedMessage,
                history: history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini chat failed for session {SessionId}", session.Id);
            aiResponseText = await _msgService.GetMessageAsync(ResultCodeConst.Chat_Fail0001);
        }

        // Post-check guard
        var (isSafe, violation, sanitizedText) = ChatGuard.Validate(aiResponseText);
        if (!isSafe)
        {
            _logger.LogWarning("Chat guard blocked response for session {SessionId}: {Violation}",
                session.Id, violation);
                
            // If Hard-ban, fallback completely. If Contextual-ban, use the sanitized response.
            aiResponseText = string.IsNullOrEmpty(sanitizedText) 
                ? await ChatGuard.GetSafeFallbackAsync(_msgService)
                : sanitizedText;
        }

        // Save AI message
        var aiMsg = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = session.Id,
            SenderType = ChatSenderType.AI,
            Content = aiResponseText,
            ModelName = _geminiService.ModelName,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        await _unitOfWork.Repository<ChatMessage, Guid>().AddAsync(aiMsg);

        // Update session
        session.LastMessageAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;
        session.UpdatedBy = userId;

        // Save all in single transaction
        var saved = await _unitOfWork.SaveChangesAsync();
        if (saved <= 0)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Fail0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }

        var response = new SendMessageResponseDto
        {
            SessionId = session.Id,
            UserMessage = ToDto(userMsg),
            AiMessage = ToDto(aiMsg)
        };

        return new ServiceResult(
            ResultCodeConst.Chat_Success0001,
            await _msgService.GetMessageAsync(ResultCodeConst.Chat_Success0001),
            response);
    }

    public async Task<IServiceResult> GetSessionsAsync(Guid userId, int page, int pageSize)
    {
        if (userId == Guid.Empty)
        {
            return new ServiceResult(
                ResultCodeConst.Auth_Warning0013,
                await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013));
        }

        var allSessions = await _unitOfWork.Repository<ChatSession, Guid>()
            .GetAllAsync(tracked: false);

        var sessions = allSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.LastMessageAt ?? s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ChatSessionDto
            {
                Id = s.Id,
                Title = s.Title,
                LastMessageAt = s.LastMessageAt,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt
            })
            .ToList();

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            sessions);
    }

    public async Task<IServiceResult> GetMessagesAsync(Guid userId, Guid sessionId, int page, int pageSize)
    {
        var session = await _unitOfWork.Repository<ChatSession, Guid>().GetByIdAsync(sessionId);
        if (session == null || session.UserId != userId)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));
        }

        var allMessages = await _unitOfWork.Repository<ChatMessage, Guid>()
            .GetAllAsync(tracked: false);

        var messages = allMessages
            .Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => ToDto(m))
            .ToList();

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            messages);
    }

    #endregion

    #region RAG — Context Building

    /// <summary>
    /// Build context packet from DB retrieval based on user intent.
    /// Only fetches related snakes, protocols and general prohibitions to save tokens and improve accuracy.
    /// </summary>
    private async Task<string> BuildContextPacketAsync(string userMessage)
    {
        var sb = new StringBuilder();
        var msgLower = userMessage.ToLowerInvariant();

        // ── 1. Retrieval: Identify snakes mentioned ──
        var snakes = await _unitOfWork.Repository<Snake, Guid>().GetAllAsync(tracked: false);
        var activeSnakes = snakes.Where(s => s.IsActive).ToList();

        var matchedSnakes = activeSnakes.Where(s =>
            msgLower.Contains(s.CommonName.ToLowerInvariant()) ||
            (!string.IsNullOrEmpty(s.ScientificName) && msgLower.Contains(s.ScientificName.ToLowerInvariant()))
        ).ToList();

        var matchedToxinGroups = new HashSet<ToxinGroup>(matchedSnakes.Select(s => s.ToxinGroup));

        // ── 2. Retrieval: Identify symptoms (if no snakes found) ──
        if (matchedSnakes.Count == 0)
        {
            var symptomKeywords = new Dictionary<string, ToxinGroup>
            {
                { "sụp mí", ToxinGroup.Neurotoxin },
                { "nặng mi", ToxinGroup.Neurotoxin },
                { "khó thở", ToxinGroup.Neurotoxin },
                { "yếu cơ", ToxinGroup.Neurotoxin },
                { "liệt", ToxinGroup.Neurotoxin },
                
                { "chảy máu", ToxinGroup.Hemotoxin },
                { "không cầm máu", ToxinGroup.Hemotoxin },
                
                // Tissue injury symptoms mapped to both/either Hemotoxin or Mixed 
                // Currently defaulting to Hemotoxin as it largely covers tissue injuries
                { "bọng nước", ToxinGroup.Hemotoxin }, 
                { "hoại tử", ToxinGroup.Hemotoxin },
                { "đau buốt", ToxinGroup.Hemotoxin },
                { "sưng nề", ToxinGroup.Hemotoxin }
            };

            foreach (var kvp in symptomKeywords)
            {
                if (msgLower.Contains(kvp.Key))
                {
                    matchedToxinGroups.Add(kvp.Value);
                }
            }
        }

        // If still no groups identified, default to Unknown
        if (matchedToxinGroups.Count == 0)
        {
            matchedToxinGroups.Add(ToxinGroup.Unknown);
        }

        // ── 3. Build Context: Snakes ──
        if (matchedSnakes.Count > 0)
        {
            sb.AppendLine("=== THÔNG TIN LOÀI RẮN NHẬN DIỆN ĐƯỢC ===");
            foreach (var snake in matchedSnakes)
            {
                sb.AppendLine($"• {snake.CommonName} ({snake.ScientificName})");
                sb.AppendLine($"  Nhóm độc: {snake.ToxinGroup}");
                sb.AppendLine($"  Mức nguy hiểm: {snake.ToxicityLevel}");
                if (!string.IsNullOrEmpty(snake.TypicalSymptoms))
                    sb.AppendLine($"  Dấu hiệu nguy hiểm (Red Flags): {snake.TypicalSymptoms}");
                sb.AppendLine();
            }
        }
        else if (matchedToxinGroups.Any(g => g != ToxinGroup.Unknown))
        {
            sb.AppendLine("=== DỰ ĐOÁN NHÓM ĐỘC DỰA TRÊN TRIỆU CHỨNG ===");
            sb.AppendLine($"Khả năng cao thuộc nhóm: {string.Join(", ", matchedToxinGroups)}");
            sb.AppendLine();
        }

        // ── 4. Build Context: Protocols & Prohibitions ──
        var firstAidItems = await _unitOfWork.Repository<FirstAidDetail, Guid>()
            .GetAllAsync(tracked: false);

        var protocols = firstAidItems
            .Where(f => f.SnakeId == null
                     && (matchedToxinGroups.Contains(f.ToxinGroup) || f.ToxinGroup == ToxinGroup.Unknown)
                     && f.ToxinGroup != ToxinGroup.GeneralProhibition
                     && f.LanguageCode == SystemLanguage.Vietnamese)
            .GroupBy(f => f.ToxinGroup)
            .OrderBy(g => g.Key);

        sb.AppendLine("=== HƯỚNG DẪN SƠ CỨU ===");
        if (!protocols.Any())
        {
            sb.AppendLine("Áp dụng sơ cứu chung: Bất động vùng cắn, không tự xử lý, đi viện lập tức.");
        }
        else
        {
            foreach (var group in protocols)
            {
                if (matchedToxinGroups.Contains(group.Key) || matchedToxinGroups.Contains(ToxinGroup.Unknown))
                {
                    sb.AppendLine($"--- Sơ cứu cho nhóm {group.Key} ---");
                    foreach (var step in group.OrderBy(s => s.StepOrder))
                    {
                        sb.AppendLine($"  Bước {step.StepOrder}: {step.Title}");
                        if (!string.IsNullOrEmpty(step.ContentMarkdown))
                            sb.AppendLine($"    {step.ContentMarkdown}");
                    }
                }
                sb.AppendLine();
            }
        }

        var prohibitions = firstAidItems
            .Where(f => f.ToxinGroup == ToxinGroup.GeneralProhibition
                     && f.SnakeId == null
                     && f.LanguageCode == SystemLanguage.Vietnamese)
            .OrderBy(f => f.StepOrder)
            .ToList();

        sb.AppendLine("=== NHỮNG ĐIỀU TUYỆT ĐỐI KHÔNG ĐƯỢC LÀM ===");
        foreach (var item in prohibitions)
        {
            sb.AppendLine($"❌ {item.Title}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// System prompt with RAG guardrails — constrains Gemini to ONLY use provided data.
    /// </summary>
    private static string BuildSystemPrompt()
    {
        return """
            Bạn là trợ lý y tế SFARS chuyên tư vấn về sơ cứu rắn cắn tại Việt Nam.

            QUY TẮC BẮT BUỘC:
            1. CHỈ đưa ra lời khuyên dựa trên [DỮ LIỆU HỆ THỐNG] (THÔNG TIN LOÀI RẮN, SƠ CỨU, ĐIỀU CẤM).
            2. KHÔNG tự bịa thêm tên thuốc, liều lượng, hay phương pháp điều trị.
            3. Luôn phải cảnh báo người bệnh: ĐẾN NGAY BỆNH VIỆN - ĐÓ LÀ CÁCH DUY NHẤT CỨU SỐNG.
            4. Phải liệt kê rõ những việc KHÔNG ĐƯỢC LÀM (từ Dữ liệu hệ thống).
            5. Nếu người dùng hỏi loài rắn cụ thể nhưng có mức độ nguy hiểm cao/deadly, phải nhấn mạnh Dấu hiệu nguy hiểm (Red Flags).
            6. Trả lời bằng tiếng Việt, thân thiện, rõ ràng (dùng bullet points), và hành động quyết đoán. Không dài dòng.
            """;
    }

    #endregion

    #region Helpers

    private async Task<List<ChatHistoryItem>?> GetConversationHistoryAsync(Guid sessionId)
    {
        var allMessages = await _unitOfWork.Repository<ChatMessage, Guid>()
            .GetAllAsync(tracked: false);

        var recentMessages = allMessages
            .Where(m => m.ChatSessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(MaxHistoryMessages)
            .OrderBy(m => m.CreatedAt)
            .ToList();

        if (!recentMessages.Any())
            return null;

        return recentMessages.Select(m => new ChatHistoryItem(
            Role: m.SenderType == ChatSenderType.User ? "user" : "model",
            Content: m.Content
        )).ToList();
    }

    private static ChatMessageDto ToDto(ChatMessage m) => new()
    {
        Id = m.Id,
        SenderType = m.SenderType,
        Content = m.Content,
        ModelName = m.ModelName,
        CreatedAt = m.CreatedAt
    };

    #endregion
}