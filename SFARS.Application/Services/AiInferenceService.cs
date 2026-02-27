using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFARS.Application.Common;
using SFARS.Application.Dtos.AiInference;
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

    // [Gemini AI] Commented out — first-aid data now sourced from DB (FirstAidDetail table).
    // Kept for potential future features (chatbot, content generation).
    // private readonly IGeminiAiService _geminiService;

    public AiInferenceService(
        ISystemMessageService msgService,
        IUnitOfWork unitOfWork,
        ILogger<AiInferenceService> logger,
        IYoloInferenceService yoloService,
        IFileStorageService storageService,
        IOptions<StorageOptions> storageOptions)
    {
        _msgService = msgService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _yoloService = yoloService;
        _storageService = storageService;
        _storageOptions = storageOptions;
    }

    /// <summary>
    /// Upload media + run AI inference in a single flow.
    /// All DB writes are batched into 1 SaveChangesAsync for optimal performance.
    /// </summary>
    public async Task<IServiceResult> AnalyzeAsync(
        Guid userId,
        Guid incidentId,
        Stream imageStream,
        string fileName,
        string contentType,
        long fileSize,
        MediaType mediaType)
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
        if (fileSize <= 0 || fileSize > storageOpt.MaxUploadBytes)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0008,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0008)
            );
        }

        if (!IsAllowedImageType(contentType, storageOpt))
        {
            return new ServiceResult(
                ResultCodeConst.AI_Warning0004,
                await _msgService.GetMessageAsync(ResultCodeConst.AI_Warning0004)
            );
        }

        try
        {
            var now = DateTime.UtcNow;

            using var bufferStream = new MemoryStream();
            await imageStream.CopyToAsync(bufferStream);
            var imageBytes = bufferStream.ToArray();

            FileUploadResult uploadResult;
            var folder = string.Format(storageOpt.IncidentMediaFolderFormat, incidentId);
            using (var uploadStream = new MemoryStream(imageBytes))
            {
                uploadResult = await _storageService.UploadAsync(uploadStream, fileName, folder, contentType);
            }

            using var yoloStream = new MemoryStream(imageBytes);
            var yoloPredictions = await _yoloService.InferAsync(yoloStream, topK: 3);
            if (!yoloPredictions.Any())
            {
                return new ServiceResult(
                    ResultCodeConst.AI_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.AI_Warning0001)
                );
            }

            var topPrediction = yoloPredictions.First();
            var primarySnake = await FindSnakeByClassName(topPrediction.ClassName);

            if (primarySnake == null)
            {
                _logger.LogWarning("Snake not found for class: {ClassName}", topPrediction.ClassName);
                return new ServiceResult(
                    ResultCodeConst.AI_Warning0005,
                    await _msgService.GetMessageAsync(ResultCodeConst.AI_Warning0005)
                );
            }

            var firstAidSteps = await GetFirstAidStepsAsync(primarySnake.ToxinGroup);
            var prohibitions = await GetProhibitionsAsync();


            var media = new IncidentMedia
            {
                Id = Guid.NewGuid(),
                IncidentId = incidentId,
                MediaUrl = uploadResult.Url,
                MediaType = mediaType,
                CreatedAt = now,
                CreatedBy = userId
            };
            await _unitOfWork.Repository<IncidentMedia, Guid>().AddAsync(media);

            var aiInference = new AiInference
            {
                Id = Guid.NewGuid(),
                IncidentId = incidentId,
                IncidentMediaId = media.Id,
                ModelName = "snake-cls-v1",
                ModelVersion = "1.0.0",
                TopK = 3,
                SelectedSnakeId = primarySnake.Id,
                SelectedConfidence = topPrediction.Confidence,
                SelectedToxinGroup = primarySnake.ToxinGroup,
                DecisionRule = "Top1",
                CreatedAt = now,
                CreatedBy = userId
            };
            await _unitOfWork.Repository<AiInference, Guid>().AddAsync(aiInference);

            int rank = 1;
            foreach (var prediction in yoloPredictions)
            {
                var snake = await FindSnakeByClassName(prediction.ClassName);
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

            incident.CurrentAiInferenceId = aiInference.Id;
            incident.SnakeId = primarySnake.Id;
            incident.AiPredictionResult = primarySnake.CommonName;
            incident.AiConfidenceScore = topPrediction.Confidence;
            incident.UpdatedAt = now;
            incident.UpdatedBy = userId;

            var existingChat = await _unitOfWork.Repository<IncidentChat, Guid>()
                .GetAllAsync(tracked: true);
            var chat = existingChat.FirstOrDefault(c => c.IncidentId == incidentId);

            if (chat != null)
            {
                var chatMessage = new IncidentChatMessage
                {
                    Id = Guid.NewGuid(),
                    ChatId = chat.Id,
                    SenderType = ChatSenderType.AI,
                    SenderId = null,
                    AiInferenceId = aiInference.Id,
                    Content = BuildAiChatInitialMessage(primarySnake, topPrediction.Confidence),
                    ModelName = "snake-cls-v1",
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

            var resultDto = new AiInferenceResultDto
            {
                InferenceId = aiInference.Id,
                PrimarySnake = new SnakeCandidateDto
                {
                    SnakeId = primarySnake.Id,
                    ScientificName = primarySnake.ScientificName,
                    CommonName = primarySnake.CommonName,
                    Confidence = topPrediction.Confidence,
                    ToxicityLevel = primarySnake.ToxicityLevel,
                    ToxinGroup = primarySnake.ToxinGroup,
                    DangerSummary = GetDangerSummary(primarySnake.ToxicityLevel),
                    TypicalSymptoms = primarySnake.TypicalSymptoms
                },
                FirstAidSteps = firstAidSteps,
                Prohibitions = prohibitions,
                OtherCandidates = await BuildOtherCandidateDtos(yoloPredictions.Skip(1).ToList()),
                Note = firstAidSteps.Count == 0
                    ? await _msgService.GetMessageAsync(ResultCodeConst.AI_Warning0006)
                    : null,
                AnalyzedAt = aiInference.CreatedAt
            };

            return new ServiceResult(
                ResultCodeConst.AI_Success0001,
                string.Format(
                    await _msgService.GetMessageAsync(ResultCodeConst.AI_Success0001),
                    primarySnake.CommonName,
                    (topPrediction.Confidence * 100).ToString("F0")),
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

        var allSnakes = await _unitOfWork.Repository<Snake, Guid>()
            .GetAllAsync(tracked: false);

        return allSnakes.FirstOrDefault(s =>
            s.ScientificName.Equals(scientificName, StringComparison.OrdinalIgnoreCase) &&
            s.IsActive);
    }

    private async Task<List<SnakeCandidateDto>> BuildOtherCandidateDtos(List<YoloPrediction> predictions)
    {
        var result = new List<SnakeCandidateDto>();
        foreach (var pred in predictions)
        {
            var snake = await FindSnakeByClassName(pred.ClassName);
            if (snake != null)
            {
                result.Add(new SnakeCandidateDto
                {
                    SnakeId = snake.Id,
                    ScientificName = snake.ScientificName,
                    CommonName = snake.CommonName,
                    Confidence = pred.Confidence,
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
    {
        return level switch
        {
            SnakeRiskLevel.Deadly => "CỰC KỲ NGUY HIỂM",
            SnakeRiskLevel.HighlyVenomous => "NGUY HIỂM",
            SnakeRiskLevel.MildlyVenomous => "ÍT ĐỘC",
            SnakeRiskLevel.NonVenomous => "KHÔNG ĐỘC",
            _ => "CẦN KIỂM TRA"
        };
    }

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
               $"Độ tin cậy: {confidence:P0}\n" +
               $"Mức độ: {dangerSummary}\n" +
               $"Nhóm độc: {snake.ToxinGroup.ToString()}\n\n" +
               $"Sơ cứu đã được hướng dẫn ở trên.\n" +
               $"Hãy theo dõi và báo lại nếu xuất hiện triệu chứng mới.";
    }

    #endregion
}