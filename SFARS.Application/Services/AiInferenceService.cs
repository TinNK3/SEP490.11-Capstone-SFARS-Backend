using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos.AiInference;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Application.Services;

/// <summary>
/// AI inference service for snake detection and first aid recommendations
/// </summary>
public class AiInferenceService : IAiInferenceService
{
    private readonly ISystemMessageService _msgService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AiInferenceService> _logger;
    private readonly IYoloInferenceService _yoloService;
    private readonly IGeminiAiService _geminiService;
    private readonly IFileStorageService _storageService;

    public AiInferenceService(
        ISystemMessageService msgService,
        IUnitOfWork unitOfWork,
        ILogger<AiInferenceService> logger,
        IYoloInferenceService yoloService,
        IGeminiAiService geminiService,
        IFileStorageService storageService)
    {
        _msgService = msgService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _yoloService = yoloService;
        _geminiService = geminiService;
        _storageService = storageService;
    }

    public async Task<IServiceResult> CreateInferenceAsync(
        Guid userId,
        Guid incidentId,
        Guid incidentMediaId)
    {
        // Validate inputs
        if (userId == Guid.Empty)
        {
            return new ServiceResult(
                ResultCodeConst.Auth_Warning0013,
                await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013)
            );
        }

        if (incidentId == Guid.Empty || incidentMediaId == Guid.Empty)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001)
            );
        }

        // Get incident
        var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);
        if (incident == null)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002)
            );
        }

        // Check ownership
        if (incident.VictimId != userId)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0007,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0007)
            );
        }

        // Get media
        var media = await _unitOfWork.Repository<IncidentMedia, Guid>().GetByIdAsync(incidentMediaId);
        if (media == null || media.IncidentId != incidentId)
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0002,
                "Media not found or does not belong to this incident"
            );
        }

        try
        {
            // Download image from storage
            using var imageStream = await DownloadImageAsync(media.MediaUrl);

            // Run YOLO inference
            var yoloPredictions = await _yoloService.InferAsync(imageStream, topK: 3);
            if (!yoloPredictions.Any())
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    "AI không thể phân tích ảnh này. Vui lòng thử ảnh khác."
                );
            }

            // Map YOLO predictions to Snake entities
            var topPrediction = yoloPredictions.First();
            var primarySnake = await FindSnakeByClassName(topPrediction.ClassName);

            if (primarySnake == null)
            {
                _logger.LogWarning("Snake not found for class: {ClassName}", topPrediction.ClassName);
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    "Không tìm thấy thông tin loài rắn này trong hệ thống"
                );
            }

            // Build Gemini request
            var geminiRequest = new GeminiAnalysisRequest(
                PrimarySnakeName: primarySnake.CommonName,
                PrimarySnakeScientificName: primarySnake.ScientificName,
                ToxicityLevel: primarySnake.ToxicityLevel.ToString(),
                ToxinGroup: primarySnake.ToxinGroup.ToString(),
                Confidence: topPrediction.Confidence,
                OtherCandidates: await BuildOtherCandidates(yoloPredictions.Skip(1).ToList())
            );

            // Call Gemini for enrichment
            var geminiResult = await _geminiService.AnalyzeSnakeBiteAsync(geminiRequest);

            // Create AiInference record
            var aiInference = new AiInference
            {
                Id = Guid.NewGuid(),
                IncidentId = incidentId,
                IncidentMediaId = incidentMediaId,
                ModelName = "snake-cls-v1",
                ModelVersion = "1.0.0",
                TopK = 3,
                SelectedSnakeId = primarySnake.Id,
                SelectedConfidence = topPrediction.Confidence,
                SelectedToxinGroup = primarySnake.ToxinGroup,
                DecisionRule = "Top1",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };

            await _unitOfWork.Repository<AiInference, Guid>().AddAsync(aiInference);

            // Create candidates
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
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userId
                    };
                    await _unitOfWork.Repository<AiInferenceCandidate, Guid>().AddAsync(candidate);
                }
            }

            // Update incident
            incident.CurrentAiInferenceId = aiInference.Id;
            incident.AiPredictionResult = primarySnake.CommonName;
            incident.AiConfidenceScore = topPrediction.Confidence;
            incident.UpdatedAt = DateTime.UtcNow;
            incident.UpdatedBy = userId;

            var saveResult = await _unitOfWork.SaveChangesAsync();
            if (saveResult <= 0)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001)
                );
            }

            // Build response DTO
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
                    DangerSummary = geminiResult.DangerSummary
                },
                FirstAidSteps = geminiResult.FirstAidSteps.Select(s => new FirstAidStepDto
                {
                    StepOrder = s.StepOrder,
                    Title = s.Title,
                    Content = s.Content
                }).ToList(),
                OtherCandidates = await BuildOtherCandidateDtos(yoloPredictions.Skip(1).ToList()),
                AiNote = geminiResult.AiNote,
                AnalyzedAt = aiInference.CreatedAt
            };

            return new ServiceResult(
                ResultCodeConst.SYS_Success0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001),
                resultDto
            );
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("FileStorage:") || ex.Message.StartsWith("AI inference"))
        {
            _logger.LogWarning(ex, "Expected error during AI inference");
            return new ServiceResult(
                ResultCodeConst.SYS_Fail0001,
                ex.Message
            );
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during AI inference");
            return new ServiceResult(
                ResultCodeConst.SYS_Fail0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001)
            );
        }
    }

    private async Task<Stream> DownloadImageAsync(string mediaUrl)
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(mediaUrl);
        response.EnsureSuccessStatusCode();
        
        var memoryStream = new MemoryStream();
        await response.Content.CopyToAsync(memoryStream);
        memoryStream.Position = 0;
        return memoryStream;
    }

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

    private async Task<List<AlternativeSnake>> BuildOtherCandidates(List<YoloPrediction> predictions)
    {
        var result = new List<AlternativeSnake>();
        foreach (var pred in predictions)
        {
            var snake = await FindSnakeByClassName(pred.ClassName);
            if (snake != null)
            {
                result.Add(new AlternativeSnake(
                    CommonName: snake.CommonName,
                    ScientificName: snake.ScientificName,
                    Confidence: pred.Confidence
                ));
            }
        }
        return result;
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
                    DangerSummary = GetDangerSummary(snake.ToxicityLevel)
                });
            }
        }
        return result;
    }

    private string GetDangerSummary(SnakeRiskLevel level)
    {
        return level switch
        {
            SnakeRiskLevel.Deadly => "⚠️ CỰC KỲ NGUY HIỂM",
            SnakeRiskLevel.HighlyVenomous => "⚠️ NGUY HIỂM",
            SnakeRiskLevel.MildlyVenomous => "⚠️ ÍT ĐỘC",
            SnakeRiskLevel.NonVenomous => "✅ KHÔNG ĐỘC",
            _ => "❓ CẦN KIỂM TRA"
        };
    }
}