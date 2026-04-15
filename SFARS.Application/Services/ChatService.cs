using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.Chat;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.Params;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

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
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Max recent messages to include as conversation history for Gemini multi-turn.
    /// </summary>
    private const int MaxHistoryMessages = 10;

    /// <summary>
    /// Max characters for auto-generated session title from first user message.
    /// </summary>
    private const int MaxTitleLength = 50;

    /// <summary>
    /// Cache TTL for embedded records (snakes, first aid).
    /// </summary>
    private static readonly TimeSpan EmbeddingCacheTtl = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Per-user rate limit: minimum seconds between messages.
    /// </summary>
    private const int MinMessageIntervalSeconds = 3;

    /// <summary>
    /// Thread-safe tracker for per-user message timestamps (rate limiting).
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, DateTime> _userLastMessageTime = new();

    #region Deterministic Triage Terms (static readonly)

    private static readonly string[] CriticalTerms =
    {
        "khó thở", "ngộp thở", "không thở được", "thở yếu", "ngừng thở", "tím môi", "lịm dần",
        "sụp mí", "mắt mờ", "nhìn mờ", "song thị", "nói khó", "nói đớ", "liệt", "yếu cơ",
        "không cử động", "co giật", "ngất", "bất tỉnh", "lơ mơ", "mất ý thức", "hôn mê",
        "máu chảy không cầm"
    };

    private static readonly string[] SymptomTerms =
    {
        "sưng nề", "sưng to", "sưng", "bọng nước", "hoại tử", "nôn nhiều", "nôn liên tục",
        "chóng mặt", "đau dữ dội", "đau tăng nhanh", "đau nhiều", "đau", "bầm tím",
        "chảy máu", "tê bì"
    };

    private static readonly string[] HighRiskSnakes =
    {
        "hổ mang", "cạp nong", "cạp nia", "vảy trầu", "chàm quạp", "hổ chúa"
    };

    private static readonly Regex[] IncidentPatterns =
    {
        new(@"\bbị\b.{0,30}\bcắn\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\brắn\b.{0,30}\bcắn\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bvết\b.{0,30}\bcắn\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bbị\b.{0,30}\bđớp\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bvết\b.{0,30}\bđớp\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bmới cắn\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bphun độc\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    private static readonly string[] AllSymptomExtractTerms =
    {
        "khó thở", "thở yếu", "tím môi", "sụp mí", "nói đớ", "song thị", "liệt", "yếu cơ",
        "sưng nề", "bọng nước", "nôn nhiều", "chóng mặt", "đau", "ngất", "lơ mơ", "máu chảy"
    };

    /// <summary>
    /// Prohibited intent patterns using word boundaries to avoid false positives.
    /// </summary>
    private static readonly Regex[] ProhibitedIntentPatterns =
    {
        new(@"\bgaro\b|\bgarô\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\brạch\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bhút nọc\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bđắp lá\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bchích\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    #endregion

    public ChatService(
        ISystemMessageService msgService,
        IUnitOfWork unitOfWork,
        IGeminiAiService geminiService,
        ILogger<ChatService> logger,
        IMemoryCache cache)
    {
        _msgService = msgService;
        _unitOfWork = unitOfWork;
        _geminiService = geminiService;
        _logger = logger;
        _cache = cache;
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

        // Rate limiting: per-user cooldown
        var now = DateTime.UtcNow;
        if (_userLastMessageTime.TryGetValue(userId, out var lastTime)
            && (now - lastTime).TotalSeconds < MinMessageIntervalSeconds)
        {
            return new ServiceResult(
                ResultCodeConst.Chat_Warning0002,
                await _msgService.GetMessageAsync(ResultCodeConst.Chat_Warning0002));
        }
        _userLastMessageTime[userId] = now;

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

        // 2. Determine State & Risk Level (Deterministic Triage)
        var history = await GetConversationHistoryAsync(session.Id);
        var currentMsgLower = trimmedMessage.ToLowerInvariant();
        
        // Extract symptoms already mentioned in this session
        var mentionedSymptoms = ExtractSymptoms(history, currentMsgLower);
        
        // Determine Risk Level (Check current and past for Critical lock)
        var riskLevel = DetermineRiskLevel(currentMsgLower, history);
        
        bool hasProhibitedIntent = ProhibitedIntentPatterns.Any(p => p.IsMatch(currentMsgLower));

        // 3. Build context packet (Semantically retrieved medical data) → PLAIN TEXT
        var medicalContext = await BuildContextAsPlainTextAsync(trimmedMessage);

        // 4. Build final context: triage state + medical data as readable text
        var prefix = ResolvePrefix(riskLevel, hasProhibitedIntent);
        var finalContextData = BuildContextText(prefix, riskLevel, mentionedSymptoms, medicalContext);

        // 5. Generate AI analysis/response using RAG context
        string aiResponseText;
        try
        {
            aiResponseText = await _geminiService.ChatWithContextAsync(
                systemPrompt: BuildSystemPrompt(prefix, riskLevel),
                contextData: finalContextData,
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

        // Enforce deterministic prefix — if AI forgot, prepend it
        aiResponseText = EnforcePrefix(aiResponseText, prefix);

        // Save AI message with context snapshot for traceability
        var aiMsg = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = session.Id,
            SenderType = ChatSenderType.AI,
            Content = aiResponseText,
            ContextSnapshotJson = finalContextData,
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

    public async Task<IServiceResult> GetSessionsAsync(Guid userId, BaseSpecParams specParams)
    {
        if (userId == Guid.Empty)
        {
            return new ServiceResult(
                ResultCodeConst.Auth_Warning0013,
                await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013));
        }

        specParams ??= new BaseSpecParams();

        var spec = new ChatSessionSpecification(userId, specParams);
        var countSpec = new ChatSessionSpecification(userId, specParams, isCount: true);

        var totalItems = await _unitOfWork.Repository<ChatSession, Guid>().CountAsync(countSpec);
        var totalPages = (int)Math.Ceiling(totalItems / (double)specParams.GetTake());

        var sessions = await _unitOfWork.Repository<ChatSession, Guid>().GetAllWithSpecAsync(spec);

        var dtos = sessions.Select(s => new ChatSessionDto
        {
            Id = s.Id,
            Title = s.Title,
            LastMessageAt = s.LastMessageAt,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt
        }).ToList();

        var paginatedResult = new PaginatedResultDto<ChatSessionDto>(dtos, specParams.GetPage(), specParams.GetTake(), totalPages, totalItems);

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            paginatedResult);
    }

    public async Task<IServiceResult> GetMessagesAsync(Guid userId, Guid sessionId, BaseSpecParams specParams)
    {
        var session = await _unitOfWork.Repository<ChatSession, Guid>().GetByIdAsync(sessionId);
        if (session == null || session.UserId != userId)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));
        }

        specParams ??= new BaseSpecParams();

        var spec = new ChatMessageSpecification(sessionId, specParams);
        var countSpec = new ChatMessageSpecification(sessionId, specParams, isCount: true);

        var totalItems = await _unitOfWork.Repository<ChatMessage, Guid>().CountAsync(countSpec);
        var totalPages = (int)Math.Ceiling(totalItems / (double)specParams.GetTake());

        var sessionMessages = await _unitOfWork.Repository<ChatMessage, Guid>().GetAllWithSpecAsync(spec);

        var messages = sessionMessages.Select(m => ToDto(m)).ToList();

        var paginatedResult = new PaginatedResultDto<ChatMessageDto>(messages, specParams.GetPage(), specParams.GetTake(), totalPages, totalItems);

        return new ServiceResult(
            ResultCodeConst.SYS_Success0002,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
            paginatedResult);
    }

    public async Task<IServiceResult> DeleteSessionAsync(Guid userId, Guid sessionId)
    {
        if (userId == Guid.Empty)
        {
            return new ServiceResult(
                ResultCodeConst.Auth_Warning0013,
                await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013));
        }

        var session = await _unitOfWork.Repository<ChatSession, Guid>().GetByIdAsync(sessionId);
        
        if (session == null || session.UserId != userId)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0002,
                string.Format(await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002), "Phiên chat"));
        }

        session.IsActive = false;
        session.UpdatedAt = DateTime.UtcNow;
        session.UpdatedBy = userId;

        _unitOfWork.Repository<ChatSession, Guid>().Update(session);
        var saved = await _unitOfWork.SaveChangesAsync();

        if (saved <= 0)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Fail0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }

        return new ServiceResult(
            ResultCodeConst.SYS_Success0003,
            string.Format(await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0003), "phiên chat"));
    }

    #endregion

    #region RAG — Context Building (Plain Text)

    /// <summary>
    /// Build context from DB retrieval as PLAIN TEXT (not JSON).
    /// Lite models parse plain text far more reliably than embedded JSON.
    /// </summary>
    private async Task<string> BuildContextAsPlainTextAsync(string userMessage)
    {
        // 1. Try semantic search first, fallback to keyword
        List<(string CommonName, string? ScientificName, string? Description, string ToxinGroup, string? TypicalSymptoms, double Score)> matchedSnakes;
        List<(string Title, string? ContentMarkdown, string ToxinGroup, int StepOrder, bool IsProhibition)> matchedProtocols;

        try
        {
            var rawVector = await _geminiService.GenerateEmbeddingAsync(userMessage);
            var userVector = NormalizeCopy(rawVector);
            (matchedSnakes, matchedProtocols) = await SemanticSearchAsync(userVector);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding failed. Using keyword fallback.");
            (matchedSnakes, matchedProtocols) = await KeywordSearchAsync(userMessage);
        }

        // 2. Format as readable plain text
        var sb = new StringBuilder();

        if (matchedSnakes.Count > 0)
        {
            sb.AppendLine("LOÀI RẮN LIÊN QUAN (từ cơ sở dữ liệu):");
            foreach (var s in matchedSnakes)
            {
                sb.AppendLine($"• {s.CommonName} ({s.ScientificName ?? "N/A"}) — Nhóm độc: {s.ToxinGroup}");
                if (!string.IsNullOrEmpty(s.TypicalSymptoms))
                    sb.AppendLine($"  Triệu chứng đặc trưng: {s.TypicalSymptoms}");
                if (!string.IsNullOrEmpty(s.Description))
                    sb.AppendLine($"  Mô tả: {s.Description}");
            }
            sb.AppendLine();
        }

        var instructions = matchedProtocols.Where(p => !p.IsProhibition).OrderBy(p => p.StepOrder).ToList();
        if (instructions.Count > 0)
        {
            sb.AppendLine("HƯỚNG DẪN SƠ CỨU (từ cơ sở dữ liệu):");
            foreach (var p in instructions)
            {
                sb.AppendLine($"  Bước {p.StepOrder}: {p.Title}");
                if (!string.IsNullOrEmpty(p.ContentMarkdown))
                    sb.AppendLine($"    {p.ContentMarkdown}");
            }
            sb.AppendLine();
        }

        var prohibitions = matchedProtocols.Where(p => p.IsProhibition).ToList();
        if (prohibitions.Count > 0)
        {
            sb.AppendLine("HÀNH VI CẤM (từ cơ sở dữ liệu):");
            foreach (var p in prohibitions)
            {
                sb.AppendLine($"  ⛔ {p.Title}: {p.ContentMarkdown}");
            }
        }

        if (matchedSnakes.Count == 0 && instructions.Count == 0)
        {
            sb.AppendLine("KHÔNG TÌM THẤY DỮ LIỆU PHÙ HỢP — hãy dùng kiến thức y khoa tổng quát.");
        }

        return sb.ToString();
    }

    private async Task<(
        List<(string CommonName, string? ScientificName, string? Description, string ToxinGroup, string? TypicalSymptoms, double Score)>,
        List<(string Title, string? ContentMarkdown, string ToxinGroup, int StepOrder, bool IsProhibition)>
    )> SemanticSearchAsync(float[] userVector)
    {
        var allSnakes = await GetCachedSnakesAsync();
        var matchedSnakes = allSnakes
            .Select(s => (Snake: s, Score: CosineSimilarityNormalized(userVector, DeserializeVector(s.EmbeddingJson!))))
            .Where(x => x.Score >= 0.5)
            .OrderByDescending(x => x.Score)
            .Take(3)
            .Select(x => (x.Snake.CommonName, x.Snake.ScientificName, x.Snake.Description, 
                ToxinGroup: x.Snake.ToxinGroup.ToString(), x.Snake.TypicalSymptoms, x.Score))
            .ToList();

        var detectedGroups = matchedSnakes.Select(s => s.ToxinGroup).Distinct().ToList();

        var allFirstAid = await GetCachedFirstAidAsync();
        var matchedProtocols = allFirstAid
            .Select(f => (Detail: f, Score: CosineSimilarityNormalized(userVector, DeserializeVector(f.EmbeddingJson!))))
            .Where(x => (x.Detail.SnakeId == null && detectedGroups.Contains(x.Detail.ToxinGroup.ToString())) || x.Score >= 0.6)
            .OrderByDescending(x => x.Score)
            .Take(7)
            .Select(x => (x.Detail.Title, x.Detail.ContentMarkdown, ToxinGroup: x.Detail.ToxinGroup.ToString(),
                x.Detail.StepOrder, IsProhibition: x.Detail.ToxinGroup == ToxinGroup.GeneralProhibition))
            .ToList();

        return (matchedSnakes, matchedProtocols);
    }

    private async Task<(
        List<(string CommonName, string? ScientificName, string? Description, string ToxinGroup, string? TypicalSymptoms, double Score)>,
        List<(string Title, string? ContentMarkdown, string ToxinGroup, int StepOrder, bool IsProhibition)>
    )> KeywordSearchAsync(string userMessage)
    {
        var msgLower = userMessage.ToLowerInvariant();

        var activeSnakes = await _unitOfWork.Repository<Snake, Guid>()
            .GetQueryable(tracked: false)
            .Where(s => s.IsActive)
            .ToListAsync();

        var matchedSnakes = activeSnakes.Where(s =>
            msgLower.Contains(s.CommonName.ToLowerInvariant()) ||
            (!string.IsNullOrEmpty(s.ScientificName) && msgLower.Contains(s.ScientificName.ToLowerInvariant()))
        ).Select(s => (s.CommonName, s.ScientificName, s.Description, 
            ToxinGroup: s.ToxinGroup.ToString(), s.TypicalSymptoms, Score: 1.0))
        .ToList();

        var matchedToxinGroups = new HashSet<ToxinGroup>(matchedSnakes
            .Select(s => Enum.TryParse<ToxinGroup>(s.ToxinGroup, out var g) ? g : ToxinGroup.Unknown));

        if (matchedSnakes.Count == 0)
        {
            var symptomKeywords = new Dictionary<string, ToxinGroup>
            {
                { "sụp mí", ToxinGroup.Neurotoxin }, { "khó thở", ToxinGroup.Neurotoxin },
                { "chảy máu", ToxinGroup.Hemotoxin }, { "hoại tử", ToxinGroup.Hemotoxin },
                { "sưng nề", ToxinGroup.Hemotoxin }
            };
            foreach (var kvp in symptomKeywords)
            {
                if (msgLower.Contains(kvp.Key)) matchedToxinGroups.Add(kvp.Value);
            }
        }

        if (matchedToxinGroups.Count == 0) matchedToxinGroups.Add(ToxinGroup.Unknown);

        var firstAidItems = await _unitOfWork.Repository<FirstAidDetail, Guid>()
            .GetQueryable(tracked: false)
            .Where(f => f.LanguageCode == SystemLanguage.Vietnamese && f.SnakeId == null)
            .ToListAsync();

        var protocols = firstAidItems
            .Where(f => matchedToxinGroups.Contains(f.ToxinGroup) || f.ToxinGroup == ToxinGroup.Unknown)
            .Select(f => (f.Title, f.ContentMarkdown, ToxinGroup: f.ToxinGroup.ToString(),
                f.StepOrder, IsProhibition: f.ToxinGroup == ToxinGroup.GeneralProhibition))
            .OrderBy(f => f.StepOrder)
            .ToList();

        return (matchedSnakes, protocols);
    }

    #endregion

    #region Embedding Helpers

    /// <summary>
    /// Normalize a vector WITHOUT mutating the input array.
    /// </summary>
    private static float[] NormalizeCopy(float[] v)
    {
        double sum = 0;
        for (int i = 0; i < v.Length; i++) sum += v[i] * v[i];
        float invMag = (float)(1.0 / Math.Sqrt(sum));
        if (double.IsInfinity(invMag)) return v;

        var result = new float[v.Length];
        for (int i = 0; i < v.Length; i++) result[i] = v[i] * invMag;
        return result;
    }

    /// <summary>
    /// Cosine similarity for pre-normalized vectors (pure dot product).
    /// </summary>
    private static double CosineSimilarityNormalized(float[] v1, float[] v2)
    {
        if (v1.Length != v2.Length) return 0;
        double dotProduct = 0;
        for (int i = 0; i < v1.Length; i++) dotProduct += v1[i] * v2[i];
        return dotProduct;
    }

    private static float[] DeserializeVector(string json) => 
        JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>();

    #endregion

    #region Caching

    private const string CacheKeySnakes = "chat_rag_snakes_embedded";
    private const string CacheKeyFirstAid = "chat_rag_firstaid_embedded";

    private async Task<List<Snake>> GetCachedSnakesAsync()
    {
        if (_cache.TryGetValue(CacheKeySnakes, out List<Snake>? cached) && cached != null)
            return cached;

        var snakes = await _unitOfWork.Repository<Snake, Guid>()
            .GetQueryable(tracked: false)
            .Where(s => s.IsActive && s.EmbeddingJson != null)
            .ToListAsync();

        _cache.Set(CacheKeySnakes, snakes, EmbeddingCacheTtl);
        return snakes;
    }

    private async Task<List<FirstAidDetail>> GetCachedFirstAidAsync()
    {
        if (_cache.TryGetValue(CacheKeyFirstAid, out List<FirstAidDetail>? cached) && cached != null)
            return cached;

        var details = await _unitOfWork.Repository<FirstAidDetail, Guid>()
            .GetQueryable(tracked: false)
            .Where(f => f.LanguageCode == SystemLanguage.Vietnamese && f.EmbeddingJson != null)
            .ToListAsync();

        _cache.Set(CacheKeyFirstAid, details, EmbeddingCacheTtl);
        return details;
    }

    #endregion

    #region Deterministic Triage

    /// <summary>
    /// Deterministic risk classification: CRITICAL > VERY_URGENT > URGENT > INFO.
    /// Risk level "sticks" — once CRITICAL is detected in history, it remains CRITICAL.
    /// </summary>
    private static string DetermineRiskLevel(string currentMsg, List<ChatHistoryItem>? history)
    {
        var msgLower = currentMsg.ToLowerInvariant();

        // 1. CRITICAL: Respiratory failure, neuro, unconscious, uncontrolled bleeding
        bool isCritical = CriticalTerms.Any(t => msgLower.Contains(t));
        if (!isCritical && history != null)
        {
            isCritical = history.Any(h => CriticalTerms.Any(t => h.Content.ToLowerInvariant().Contains(t)));
        }
        if (isCritical) return "CRITICAL";

        // 2. VERY_URGENT: Symptoms + high-risk species
        bool hasSymptomOrHighRisk = SymptomTerms.Any(t => msgLower.Contains(t)) || HighRiskSnakes.Any(t => msgLower.Contains(t));
        if (!hasSymptomOrHighRisk && history != null)
        {
            hasSymptomOrHighRisk = history.Any(h => 
                SymptomTerms.Any(t => h.Content.ToLowerInvariant().Contains(t)) || 
                HighRiskSnakes.Any(t => h.Content.ToLowerInvariant().Contains(t)));
        }

        // 3. URGENT: Confirmed bite
        bool isIncident = IncidentPatterns.Any(p => p.IsMatch(msgLower));
        if (!isIncident && history != null)
        {
            isIncident = history.Any(h => IncidentPatterns.Any(p => p.IsMatch(h.Content.ToLowerInvariant())));
        }

        if (hasSymptomOrHighRisk && isIncident) return "VERY_URGENT";
        if (isIncident) return "URGENT";

        return "INFO";
    }

    private static List<string> ExtractSymptoms(List<ChatHistoryItem>? history, string currentMsg)
    {
        var combinedText = currentMsg + (history != null ? " " + string.Join(" ", history.Select(h => h.Content)) : "");
        var lower = combinedText.ToLowerInvariant();

        return AllSymptomExtractTerms
            .Where(term => lower.Contains(term))
            .Distinct()
            .ToList();
    }

    #endregion

    #region System Prompt & Prefix

    /// <summary>
    /// Maps deterministic risk level to display prefix. Code decides — not AI.
    /// </summary>
    private static string ResolvePrefix(string riskLevel, bool hasProhibitedIntent)
    {
        if (hasProhibitedIntent) return "[CẢNH BÁO]";
        return riskLevel switch
        {
            "CRITICAL" => "[NGUY KỊCH]",
            "VERY_URGENT" => "[RẤT KHẨN CẤP]",
            "URGENT" => "[KHẨN CẤP]",
            _ => "[THÔNG TIN]"
        };
    }

    /// <summary>
    /// Ensures AI response starts with the correct prefix. If AI forgot or used
    /// a different prefix, prepend/replace it. This is the safety net.
    /// </summary>
    private static string EnforcePrefix(string aiResponse, string requiredPrefix)
    {
        if (string.IsNullOrWhiteSpace(aiResponse)) return aiResponse;

        var trimmed = aiResponse.TrimStart();
        // Already starts with required prefix
        if (trimmed.StartsWith(requiredPrefix)) return aiResponse;

        // Starts with a WRONG prefix — replace it
        string[] allPrefixes = { "[NGUY KỊCH]", "[RẤT KHẨN CẤP]", "[KHẨN CẤP]", "[THÔNG TIN]", "[CẢNH BÁO]" };
        foreach (var p in allPrefixes)
        {
            if (trimmed.StartsWith(p))
            {
                return requiredPrefix + trimmed[p.Length..];
            }
        }

        // No prefix at all — prepend
        return $"{requiredPrefix} {trimmed}";
    }

    /// <summary>
    /// Builds full context text with triage state + medical data.
    /// Plain text format for maximum compatibility with lite models.
    /// </summary>
    private static string BuildContextText(string prefix, string riskLevel, List<string> mentionedSymptoms, string medicalContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"MỨC ĐỘ PHÂN LOẠI: {riskLevel}");
        sb.AppendLine($"PREFIX BẮT BUỘC: {prefix}");
        if (mentionedSymptoms.Count > 0)
            sb.AppendLine($"TRIỆU CHỨNG ĐÃ ĐỀ CẬP (không hỏi lại): {string.Join(", ", mentionedSymptoms)}");
        sb.AppendLine();
        sb.Append(medicalContext);
        return sb.ToString();
    }

    /// <summary>
    /// RAG-aware system prompt. Prefix is HARD-CODED by code, not AI-decided.
    /// Context data is PLAIN TEXT, not JSON — optimized for gemini-flash-lite.
    /// Mobile-tuned: 3-5 lines max.
    /// </summary>
    private static string BuildSystemPrompt(string prefix, string riskLevel)
    {
        var urgencyDirective = riskLevel switch
        {
            "CRITICAL" => "Đây là NGUY KỊCH. Response PHẢI hướng người dùng GỌI 115 NGAY.",
            "VERY_URGENT" => "Đây là RẤT KHẨN CẤP. Response PHẢI hướng người dùng ĐI CẤP CỨU NGAY.",
            "URGENT" => "Đây là KHẨN CẤP. Chưa loại trừ được nhiễm độc. Khuyên đi cơ sở y tế.",
            _ => "Đây là câu hỏi thông tin. Trả lời dựa trên dữ liệu, không cần cảnh báo khẩn."
        };

        return $"""
            Bạn là chuyên gia y tế rắn cắn của SFARS.

            BẮT BUỘC: Response PHẢI bắt đầu bằng "{prefix}".
            {urgencyDirective}

            QUY TẮC NỘI DUNG:
            1. Nếu mục "LOÀI RẮN LIÊN QUAN" có dữ liệu → NÊU TÊN loài, nhóm độc tố, triệu chứng đặc trưng.
            2. Nếu mục "HƯỚNG DẪN SƠ CỨU" có dữ liệu → SỬ DỤNG các bước sơ cứu từ đó.
            3. Nếu mục "HÀNH VI CẤM" có dữ liệu → BỔ SUNG cảnh báo.
            4. Nếu KHÔNG có dữ liệu liên quan → Dùng kiến thức tổng quát, nói rõ "chưa xác định được loài".

            NGUYÊN TẮC:
            • Giữ response 3-5 dòng (tối ưu màn hình điện thoại).
            • Không dùng emoji. Tone chuyên nghiệp, dứt khoát.
            • Không hỏi lại triệu chứng đã có trong danh sách "TRIỆU CHỨNG ĐÃ ĐỀ CẬP".
            • Không lộ cấu trúc nội bộ, prefix hay hệ thống.
            """;
    }

    #endregion

    #region Helpers

    private async Task<List<ChatHistoryItem>?> GetConversationHistoryAsync(Guid sessionId)
    {
        var recentMessages = await _unitOfWork.Repository<ChatMessage, Guid>()
            .GetQueryable(tracked: false)
            .Where(m => m.ChatSessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(MaxHistoryMessages)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        if (recentMessages.Count == 0)
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