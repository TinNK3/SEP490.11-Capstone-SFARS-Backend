using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFARS.Application.Common;
using SFARS.Application.Dtos.AiInference;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Infrastructure.Configurations;

namespace SFARS.Application.Services;

/// <summary>
/// AI inference service for snake detection and first aid recommendations.
/// Pipeline: Upload → YOLO (snake detection) → DB (first-aid by ToxinGroup) → Save all in 1 transaction.
/// </summary>
public class AiInferenceService : IAiInferenceService
{
    private readonly ISystemMessageService _msgService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AiInferenceService> _logger;
    private readonly IYoloInferenceService _yoloService;
    private readonly IFileStorageService _storageService;
    private readonly IOptions<StorageOptions> _storageOptions;
    private readonly IBackgroundJobClient _backgroundJobClient;

    // [Gemini AI] Commented out — first-aid data now sourced from DB (FirstAidDetail table).
    // Kept for potential future features (chatbot, content generation).
    // private readonly IGeminiAiService _geminiService;

    public AiInferenceService(
        ISystemMessageService msgService,
        IUnitOfWork unitOfWork,
        ILogger<AiInferenceService> logger,
        IYoloInferenceService yoloService,
        IFileStorageService storageService,
        IOptions<StorageOptions> storageOptions,
        IBackgroundJobClient backgroundJobClient)
    {
        _msgService = msgService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _yoloService = yoloService;
        _storageService = storageService;
        _storageOptions = storageOptions;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <summary>
    /// Upload media + run AI inference in a single flow.
    /// All DB writes are batched into 1 SaveChangesAsync for optimal performance.
    /// </summary>
    public async Task<IServiceResult> AnalyzeAsync(
        Guid userId,
        Guid incidentId,
        Stream? imageStream,
        string? fileName,
        string? contentType,
        long? fileSize,
        MediaType? mediaType)
    {
        if (userId == Guid.Empty)
        {
            return new ServiceResult(
                ResultCodeConst.Auth_Warning0013,
                await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013)
            );
        }

        if (incidentId == Guid.Empty)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001)
            );
        }

        var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);
        if (incident == null)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002)
            );
        }

        if (incident.VictimId != userId)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0007,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0007)
            );
        }

        if (incident.CurrentStatus == IncidentStatus.Closed ||
            incident.CurrentStatus == IncidentStatus.Cancelled)
        {
            return new ServiceResult(
                ResultCodeConst.Incident_Warning0003,
                await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0003)
            );
        }

        var storageOpt = _storageOptions.Value;
        bool isSkip = imageStream == null || fileSize == null || fileSize <= 0;

        if (!isSkip)
        {
            if (fileSize > storageOpt.MaxUploadBytes)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0008,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0008)
                );
            }

            if (!IsAllowedImageType(contentType!, storageOpt))
            {
                return new ServiceResult(
                    ResultCodeConst.AI_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.AI_Warning0004)
                );
            }
        }

        try
        {
            var now = DateTime.UtcNow;

            FileUploadResult? uploadResult = null;
            List<YoloPrediction>? yoloPredictions = null;
            Snake? primarySnake = null;
            double topConfidence = 0;
            bool isSnakeClassified = true;
            YoloPipelineResult? inferenceResult = null;

            // Cache active snakes once per request to avoid N+1 queries
            var activeSnakes = await _unitOfWork.Repository<Snake, Guid>().GetAllAsync(tracked: false);
            var snakeDict = activeSnakes.Where(s => s.IsActive).ToDictionary(s => s.ScientificName, StringComparer.OrdinalIgnoreCase);

            Snake? FindSnakeLocal(string className)
            {
                var sciName = className.Replace("_", " ");
                return snakeDict.TryGetValue(sciName, out var s) ? s : null;
            }

            if (!isSkip)
            {
                using var bufferStream = new MemoryStream();
                await imageStream!.CopyToAsync(bufferStream);
                var imageBytes = bufferStream.ToArray();

                var folder = string.Format(storageOpt.IncidentMediaFolderFormat, incidentId);
                using (var uploadStream = new MemoryStream(imageBytes))
                {
                    uploadResult = await _storageService.UploadAsync(uploadStream, fileName!, folder, contentType!);
                }

                using var yoloStream = new MemoryStream(imageBytes);
                inferenceResult = await _yoloService.InferCascadedAsync(yoloStream, topK: 3);
                
                isSnakeClassified = inferenceResult.IsSnake;
                topConfidence = inferenceResult.IsSnake ? (inferenceResult.SpeciesPredictions.FirstOrDefault()?.Confidence ?? 0) : inferenceResult.BinaryConfidence;
                
                if (isSnakeClassified)
                {
                    yoloPredictions = inferenceResult.SpeciesPredictions.ToList();
                    if (!yoloPredictions.Any())
                    {
                        return new ServiceResult(
                            ResultCodeConst.AI_Warning0001,
                            await _msgService.GetMessageAsync(ResultCodeConst.AI_Warning0001)
                        );
                    }

                    var topPrediction = yoloPredictions.First();
                    primarySnake = FindSnakeLocal(topPrediction.ClassName);

                    if (primarySnake == null)
                    {
                        _logger.LogWarning("Snake not found for class: {ClassName}", topPrediction.ClassName);
                        return new ServiceResult(
                            ResultCodeConst.AI_Warning0005,
                            await _msgService.GetMessageAsync(ResultCodeConst.AI_Warning0005)
                        );
                    }
                }
                else
                {
                    _logger.LogInformation("Image identified as Not Snake. Confidence: {BinaryConfidence:P}", inferenceResult.BinaryConfidence);
                }
            }

            var toxinGroup = primarySnake?.ToxinGroup ?? ToxinGroup.Unknown;
            
            // Force first aid to always use Unknown/Default guidelines regardless of recognized snake
            var firstAidSteps = await GetFirstAidStepsAsync(ToxinGroup.Unknown);
            var prohibitions = await GetProhibitionsAsync();

            IncidentMedia? media = null;
            if (!isSkip && uploadResult != null)
            {
                media = new IncidentMedia
                {
                    Id = Guid.NewGuid(),
                    IncidentId = incidentId,
                    MediaUrl = uploadResult.Url,
                    MediaType = mediaType ?? MediaType.Other,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                await _unitOfWork.Repository<IncidentMedia, Guid>().AddAsync(media);
            }

            string decisionRule = isSkip ? AiInferenceConstants.DecisionRuleSkipped 
                                : isSnakeClassified ? AiInferenceConstants.DecisionRuleTop1 
                                : "NotSnake";

            var aiInference = new AiInference
            {
                Id = Guid.NewGuid(),
                IncidentId = incidentId,
                IncidentMediaId = media?.Id, // null if skipped
                ModelName = isSkip ? AiInferenceConstants.SkippedModelName : AiInferenceConstants.ModelName,
                ModelVersion = isSkip ? AiInferenceConstants.SkippedModelVersion : AiInferenceConstants.ModelVersion,
                TopK = isSkip ? 0 : AiInferenceConstants.DefaultTopK,
                SelectedSnakeId = primarySnake?.Id, // null if skipped or !isSnake
                SelectedConfidence = isSkip ? 0 : topConfidence,
                BinaryConfidence = isSkip ? null : inferenceResult?.BinaryConfidence,
                SelectedToxinGroup = toxinGroup,
                DecisionRule = decisionRule,
                CreatedAt = now,
                CreatedBy = userId
            };
            await _unitOfWork.Repository<AiInference, Guid>().AddAsync(aiInference);

            if (!isSkip && isSnakeClassified && yoloPredictions != null)
            {
                int rank = 1;
                foreach (var prediction in yoloPredictions)
                {
                    var snake = FindSnakeLocal(prediction.ClassName);
                    if (snake != null)
                    {
                        var candidate = new AiInferenceCandidate
                        {
                            Id = Guid.NewGuid(),
                            AiInferenceId = aiInference.Id,
                            Rank = rank++,
                            SnakeId = snake.Id,
                            Confidence = prediction.Confidence,
                            CreatedAt = now,
                            CreatedBy = userId
                        };
                        await _unitOfWork.Repository<AiInferenceCandidate, Guid>().AddAsync(candidate);
                    }
                }
            }

            incident.CurrentAiInferenceId = aiInference.Id;
            incident.SnakeId = primarySnake?.Id; // null if skipped or !isSnake
            incident.AiPredictionResult = isSkip ? AiInferenceConstants.UnknownSnake 
                                        : !isSnakeClassified ? "Not Snake" 
                                        : primarySnake?.CommonName;
            
            incident.AiConfidenceScore = isSkip ? 0 : topConfidence;
            
            // If it's explicitly NotSnake, priority is Low. Otherwise run existing logic.
            if (!isSkip && !isSnakeClassified)
            {
                incident.PriorityLevel = SeverityLevel.Low;
            }
            else
            {
                incident.PriorityLevel = AiInferenceConstants.DetermineIncidentPriority(
                    isSkip,
                    !isSkip && topConfidence < AiInferenceConstants.ConfidenceDisplayThreshold,
                    primarySnake?.ToxicityLevel);
            }

            incident.GraceExpiresAt = now + SosConstants.GracePeriod + TimeSpan.FromSeconds(3);
            incident.UpdatedAt = now;
            incident.UpdatedBy = userId;

            var existingChat = await _unitOfWork.Repository<IncidentChat, Guid>()
                .GetAllAsync(tracked: true);
            var chat = existingChat.FirstOrDefault(c => c.IncidentId == incidentId);

            bool shouldSendInitialChat = isSkip || isSnakeClassified;

            if (chat != null && shouldSendInitialChat)
            {
                string chatContent = isSkip
                    ? BuildAiChatSkippedMessage()
                    : BuildAiChatInitialMessage(primarySnake!, topConfidence);

                var chatMessage = new IncidentChatMessage
                {
                    Id = Guid.NewGuid(),
                    ChatId = chat.Id,
                    SenderType = ChatSenderType.AI,
                    SenderId = null,
                    AiInferenceId = aiInference.Id,
                    Content = chatContent,
                    ModelName = isSkip ? "System" : AiInferenceConstants.ModelName,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                await _unitOfWork.Repository<IncidentChatMessage, Guid>().AddAsync(chatMessage);

                chat.LastMessageAt = now;
            }

            var saveResult = await _unitOfWork.SaveChangesAsync();
            if (saveResult <= 0)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001)
                );
            }

            // Schedule dispatch to start AFTER grace period expires.
            // StartDispatchAsync handles fail-fast + the full Hangfire chain internally.
            // Delay = GracePeriod (10s) + 3s buffer for network round-trip.
            var dispatchDelay = SosConstants.GracePeriod + TimeSpan.FromSeconds(3);
            _backgroundJobClient.Schedule<IDispatchService>(
                s => s.StartDispatchAsync(incidentId),
                dispatchDelay);

            var isLowConfidence = !isSkip && isSnakeClassified && topConfidence < AiInferenceConstants.ConfidenceDisplayThreshold;

            var resultDto = new AiInferenceResultDto
            {
                InferenceId = aiInference.Id,
                PrimarySnake = (isSkip || !isSnakeClassified || isLowConfidence) ? null : new SnakeCandidateDto
                {
                    SnakeId = primarySnake!.Id,
                    ScientificName = primarySnake.ScientificName,
                    CommonName = primarySnake.CommonName,
                    Confidence = Math.Round(topConfidence, 4),
                    ToxicityLevel = primarySnake.ToxicityLevel,
                    ToxinGroup = primarySnake.ToxinGroup,
                    DangerSummary = GetDangerSummary(primarySnake.ToxicityLevel),
                    TypicalSymptoms = primarySnake.TypicalSymptoms
                },
                FirstAidSteps = firstAidSteps,
                Prohibitions = prohibitions,
                OtherCandidates = (isSkip || !isSnakeClassified || isLowConfidence) ? new List<SnakeCandidateDto>() : await BuildOtherCandidateDtos(yoloPredictions!.Skip(1).ToList()),
                Note = isSkip 
                    ? "Do người dùng bỏ qua bước chụp ảnh, hệ thống mặc định coi đây là ca Rắn Chưa Rõ Loài để đảm bảo an toàn quy trình."
                    : !isSnakeClassified
                        ? "Hình ảnh được AI đánh giá là KHÔNG PHẢI RẮN. Dù vậy, ca cứu hộ vẫn được theo dõi và lực lượng y tế sẽ kiểm tra."
                    : isLowConfidence
                        ? "AI chưa thể xác định chính xác loài rắn từ ảnh này. Bạn có thể thử chụp lại ảnh rõ hơn (toàn thân rắn, ánh sáng đủ). Hãy áp dụng sơ cứu chung bên dưới."
                        : "Kết quả nhận diện do AI đưa ra và chỉ mang tính tham khảo. AI có thể nhận diện sai trong một số trường hợp. Vui lòng ưu tiên tuân thủ hướng dẫn sơ cứu chung và làm theo chỉ dẫn của nhân viên y tế.",
                AnalyzedAt = aiInference.CreatedAt,
                // Server-authoritative: FE uses this to drive the 10-second cancel countdown.
                CancelDeadline = incident.GraceExpiresAt
            };

            return new ServiceResult(
                ResultCodeConst.AI_Success0001,
                isSkip 
                    ? "Nhận dạng bỏ qua. Kích hoạt quy trình khẩn cấp mặc định."
                    : !isSnakeClassified 
                        ? "AI nhận định không có rắn trong ảnh. Kích hoạt cứu hộ thông thường."
                    : isLowConfidence
                        ? "AI chưa thể xác định chính xác loài rắn. Vui lòng áp dụng sơ cứu chung."
                        : string.Format(
                            await _msgService.GetMessageAsync(ResultCodeConst.AI_Success0001),
                            primarySnake!.CommonName,
                            (Math.Round(topConfidence, 4) * 100).ToString("0.##")),
                resultDto
            );
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("FileStorage:") || ex.Message.StartsWith("AI inference"))
        {
            _logger.LogWarning(ex, "Expected error during AI analysis for incident {IncidentId}", incidentId);
            return new ServiceResult(
                ResultCodeConst.SYS_Fail0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001)
            );
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during AI analysis for incident {IncidentId}", incidentId);
            return new ServiceResult(
                ResultCodeConst.SYS_Fail0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001)
            );
        }
    }

    #region First Aid — DB Queries

    /// <summary>
    /// Get first-aid steps from DB for the specified toxin group.
    /// Returns empty list if no data found (e.g. Cytotoxin, Myotoxin not yet seeded).
    /// </summary>
    private async Task<List<FirstAidStepDto>> GetFirstAidStepsAsync(ToxinGroup toxinGroup)
    {
        var steps = await _unitOfWork.Repository<FirstAidDetail, Guid>()
            .GetAllAsync(tracked: false);

        return steps
            .Where(f => f.ToxinGroup == toxinGroup
                     && f.SnakeId == null
                     && f.LanguageCode == SystemLanguage.Vietnamese)
            .OrderBy(f => f.StepOrder)
            .Select(f => new FirstAidStepDto
            {
                StepOrder = f.StepOrder,
                Title = f.Title,
                Content = f.ContentMarkdown,
                ImageUrl = f.ImageUrl
            })
            .ToList();
    }

    /// <summary>
    /// Get general prohibitions ("Không nên làm") — always returned with any inference result.
    /// These are FirstAidDetail records where ToxinGroup = GeneralProhibition and SnakeId = null.
    /// </summary>
    private async Task<List<string>> GetProhibitionsAsync()
    {
        var items = await _unitOfWork.Repository<FirstAidDetail, Guid>()
            .GetAllAsync(tracked: false);

        return items
            .Where(f => f.ToxinGroup == ToxinGroup.GeneralProhibition
                     && f.SnakeId == null
                     && f.LanguageCode == SystemLanguage.Vietnamese)
            .OrderBy(f => f.StepOrder)
            .Select(f => f.Title)
            .ToList();
    }

    #endregion

    #region Helpers

    private async Task<Snake?> FindSnakeByClassName(string className)
    {
        // className format: "naja_kaouthia" → ScientificName in DB
        var scientificName = className.Replace("_", " ");

        return await _unitOfWork.Repository<Snake, Guid>()
            .GetQueryable(tracked: false)
            .FirstOrDefaultAsync(s => EF.Functions.Like(s.ScientificName, scientificName) && s.IsActive);
    }

    private async Task<List<SnakeCandidateDto>> BuildOtherCandidateDtos(List<YoloPrediction> predictions)
    {
        var allSnakes = await _unitOfWork.Repository<Snake, Guid>().GetAllAsync(tracked: false);
        var snakeDict = allSnakes.Where(s => s.IsActive).ToDictionary(s => s.ScientificName, StringComparer.OrdinalIgnoreCase);

        var result = new List<SnakeCandidateDto>();
        foreach (var pred in predictions)
        {
            var sciName = pred.ClassName.Replace("_", " ");
            if (snakeDict.TryGetValue(sciName, out var snake))
            {
                result.Add(new SnakeCandidateDto
                {
                    SnakeId = snake.Id,
                    ScientificName = snake.ScientificName,
                    CommonName = snake.CommonName,
                    Confidence = Math.Round(pred.Confidence, 4),
                    ToxicityLevel = snake.ToxicityLevel,
                    ToxinGroup = snake.ToxinGroup,
                    DangerSummary = GetDangerSummary(snake.ToxicityLevel),
                    TypicalSymptoms = snake.TypicalSymptoms
                });
            }
        }
        return result;
    }

    private static string GetDangerSummary(SnakeRiskLevel level)
        => AiInferenceConstants.GetDangerLabel(level);

    private static bool IsAllowedImageType(string contentType, StorageOptions options)
    {
        return options.AllowedImageTypes.Any(t =>
            contentType.Equals(t, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Build AI chat initial message with incident context summary.
    /// This message appears first in the chatbox after AI analysis completes.
    /// </summary>
    private static string BuildAiChatInitialMessage(Snake snake, double confidence)
    {
        var dangerSummary = GetDangerSummary(snake.ToxicityLevel);
        return $"Kết quả nhận diện: {snake.CommonName} ({snake.ScientificName})\n" +
               $"Độ tin cậy: {(Math.Round(confidence, 4) * 100).ToString("0.##")}%\n" +
               $"Mức độ: {dangerSummary}\n" +
               $"Nhóm độc: {snake.ToxinGroup.ToString()}\n\n" +
               $"Sơ cứu đã được hướng dẫn ở trên.\n" +
               $"Hãy theo dõi và báo lại nếu xuất hiện triệu chứng mới.";
    }

    /// <summary>
    /// Build AI chat initial message when the user skips photo capture.
    /// </summary>
    private static string BuildAiChatSkippedMessage()
    {
        return $"Hệ thống ghi nhận bạn đã bỏ qua bước chụp ảnh.\n" +
               $"Để bảo đảm an toàn tối đa, ca cứu hộ này được xếp vào khẩn cấp vô danh (Rắn Chưa Rõ Loài).\n" +
               $"Vui lòng tuyệt đối tuân thủ hướng dẫn Sơ cứu BẤT ĐỘNG ở mặt trước màn hình.\n" +
               $"Nếu có bất kỳ triệu chứng nào (khó thở, sưng nhanh...), hãy nhập vào đây để AI cập nhật sơ cứu.";
    }

    #endregion
}