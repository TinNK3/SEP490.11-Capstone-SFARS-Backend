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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SFARS.Application.Services;

/// <summary>
/// AI inference service for snake detection and first aid recommendations.
/// Pipeline: Upload → YOLO detection (Stage 1) → Crop & Pad → EfficientNetV2 classification (Stage 2) → Save.
/// </summary>
public class AiInferenceService : IAiInferenceService
{
    private readonly ISystemMessageService _msgService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AiInferenceService> _logger;
    private readonly ISnakeDetectionService _detectionService;
    private readonly IFileStorageService _storageService;
    private readonly IOptions<StorageOptions> _storageOptions;
    private readonly IOptions<YoloDetectionOptions> _yoloOptions;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ISpeciesClassificationService _classificationService;

    /// <summary>
    /// Class name used by the EfficientNetV2 classifier for non-snake objects.
    /// </summary>
    private const string NotSnakeClassName = "not_snake";

    /// <summary>
    /// Minimum confidence gap between top-1 and top-2 predictions
    /// when they have different toxicity. Below this, treat as venomous.
    /// </summary>
    private const float AmbiguousGapThreshold = 0.15f;

    public AiInferenceService(
        ISystemMessageService msgService,
        IUnitOfWork unitOfWork,
        ILogger<AiInferenceService> logger,
        ISnakeDetectionService detectionService,
        ISpeciesClassificationService classificationService,
        IFileStorageService storageService,
        IOptions<StorageOptions> storageOptions,
        IOptions<YoloDetectionOptions> yoloOptions,
        IBackgroundJobClient backgroundJobClient)
    {
        _msgService = msgService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _detectionService = detectionService;
        _classificationService = classificationService;
        _storageService = storageService;
        _storageOptions = storageOptions;
        _yoloOptions = yoloOptions;
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
            List<SpeciesPrediction>? speciesPredictions = null;
            Snake? primarySnake = null;
            double topConfidence = 0;
            bool isSnakeClassified = true;
            bool isLowConfidence = false;
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

                // ── Stage 1: YOLO Detection — detect snake & get bounding box ──
                var detection = await _detectionService.DetectAsync(imageBytes);

                _logger.LogInformation(
                    "YOLO detection: IsDetected={IsDetected}, Confidence={Confidence:P}",
                    detection.IsDetected, detection.Confidence);

                if (detection.IsDetected && detection.Box != null)
                {
                    // ── Stage 1.5: Crop & Pad (letterbox) ──
                    var yoloOpt = _yoloOptions.Value;
                    var croppedBytes = CropAndPad(imageBytes, detection.Box, yoloOpt.MarginRatio);

                    // ── Stage 2: EfficientNetV2 Species Classification ──
                    using var speciesStream = new MemoryStream(croppedBytes);
                    var classificationPredictions = await _classificationService.InferSpeciesOnlyAsync(speciesStream, topK: 3);
                    speciesPredictions = classificationPredictions.ToList();

                    var top1 = speciesPredictions.FirstOrDefault();
                    var top2 = speciesPredictions.Skip(1).FirstOrDefault();
                    var maxConfidence = top1?.Confidence ?? 0;

                    // Case 1: Classifier says "Not_Snake"
                    if (top1 != null && string.Equals(top1.ClassName, NotSnakeClassName, StringComparison.OrdinalIgnoreCase))
                    {
                        isSnakeClassified = false;
                        _logger.LogInformation("Classifier identified object as Not Snake (confidence: {Conf:P})", maxConfidence);
                    }
                    // Case 2: Too low confidence (< 50%)
                    else if (!speciesPredictions.Any() || maxConfidence < 0.50f)
                    {
                        isLowConfidence = true;
                        isSnakeClassified = false;
                        _logger.LogWarning("Classifier confidence below 50% threshold ({Confidence:P}). Marking as Unknown Snake.", maxConfidence);
                    }
                    // Case 3: Valid classification
                    else
                    {
                        topConfidence = maxConfidence;
                        primarySnake = FindSnakeLocal(top1!.ClassName);

                        if (primarySnake == null)
                        {
                            _logger.LogWarning("Snake not found in DB for class: {ClassName}", top1.ClassName);
                            return new ServiceResult(
                                ResultCodeConst.AI_Warning0005,
                                await _msgService.GetMessageAsync(ResultCodeConst.AI_Warning0005)
                            );
                        }

                        // Gap check: if top-1 and top-2 have different toxicity and gap < 15%,
                        // treat as venomous for safety (medical first-aid context)
                        if (top2 != null
                            && !string.Equals(top2.ClassName, NotSnakeClassName, StringComparison.OrdinalIgnoreCase)
                            && (maxConfidence - top2.Confidence) < AmbiguousGapThreshold)
                        {
                            var secondSnake = FindSnakeLocal(top2.ClassName);
                            if (secondSnake != null && primarySnake.ToxicityLevel != secondSnake.ToxicityLevel)
                            {
                                _logger.LogWarning(
                                    "Ambiguous prediction: {Snake1} ({Conf1:P}) vs {Snake2} ({Conf2:P}), gap={Gap:P}. Treating as venomous.",
                                    top1.ClassName, maxConfidence, top2.ClassName, top2.Confidence,
                                    maxConfidence - top2.Confidence);
                            }
                        }
                    }
                }
                else
                {
                    isSnakeClassified = false;
                    _logger.LogInformation("YOLO: No snake detected in image.");
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
                SelectedToxinGroup = toxinGroup,
                DecisionRule = decisionRule,
                CreatedAt = now,
                CreatedBy = userId
            };
            await _unitOfWork.Repository<AiInference, Guid>().AddAsync(aiInference);

            if (!isSkip && isSnakeClassified && speciesPredictions != null)
            {
                int rank = 1;
                foreach (var prediction in speciesPredictions)
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
                    ? AiInferenceConstants.ChatSkipped
                    : string.Format(
                        AiInferenceConstants.ChatInitialFormat,
                        primarySnake!.CommonName,
                        primarySnake!.ScientificName,
                        (Math.Round(topConfidence, 4) * 100).ToString("0.##"),
                        GetDangerSummary(primarySnake!.ToxicityLevel),
                        primarySnake!.ToxinGroup.ToString());

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

            isLowConfidence = !isSkip && isSnakeClassified && topConfidence < AiInferenceConstants.ConfidenceDisplayThreshold;

            string noteMsg = isSkip 
                ? AiInferenceConstants.NoteSkip
                : !isSnakeClassified 
                    ? AiInferenceConstants.NoteNotSnake
                : isLowConfidence 
                    ? AiInferenceConstants.NoteLowConf
                    : AiInferenceConstants.NoteResult;

            string actionMsg = isSkip 
                ? AiInferenceConstants.MsgSkipAction
                : !isSnakeClassified 
                    ? AiInferenceConstants.MsgNotSnakeAction
                : isLowConfidence 
                    ? AiInferenceConstants.MsgLowConfAction
                    : string.Format(
                        AiInferenceConstants.SuccessIdentifyFormat,
                        primarySnake!.CommonName,
                        (Math.Round(topConfidence, 4) * 100).ToString("0.##"));

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
                OtherCandidates = (isSkip || !isSnakeClassified || isLowConfidence) ? new List<SnakeCandidateDto>() : await BuildOtherCandidateDtos(speciesPredictions!.Skip(1).ToList()),
                Note = noteMsg,
                AnalyzedAt = aiInference.CreatedAt,
                // Server-authoritative: FE uses this to drive the 10-second cancel countdown.
                CancelDeadline = incident.GraceExpiresAt
            };

            return new ServiceResult(
                ResultCodeConst.AI_Success0001,
                actionMsg,
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
    private async Task<List<FirstAidStepDto>> GetProhibitionsAsync()
    {
        var prohibits = await _unitOfWork.Repository<FirstAidDetail, Guid>().GetAllAsync(tracked: false);

        return prohibits.Where(f => f.ToxinGroup == ToxinGroup.GeneralProhibition 
                     && f.LanguageCode == SystemLanguage.Vietnamese)
            .OrderBy(f => f.StepOrder)
            .Select(x => new FirstAidStepDto
            {
                StepOrder = x.StepOrder,
                Title = x.Title,
                Content = x.ContentMarkdown,
                ImageUrl = x.ImageUrl
            }).ToList();
    }

    /// <summary>
    /// Crop the snake region from the original image using pixel bounding box,
    /// expand by margin, then letterbox-pad to 224x224 maintaining aspect ratio.
    /// Matches the Python training pipeline: crop → resize (keep ratio) → pad black.
    /// </summary>
    private static byte[] CropAndPad(byte[] originalImageBytes, BoundingBox box, float marginRatio)
    {
        using var image = Image.Load<Rgb24>(originalImageBytes);
        int imgW = image.Width;
        int imgH = image.Height;

        int boxW = box.XMax - box.XMin;
        int boxH = box.YMax - box.YMin;
        int marginX = (int)(boxW * marginRatio);
        int marginY = (int)(boxH * marginRatio);

        int cropXMin = Math.Max(0, box.XMin - marginX);
        int cropYMin = Math.Max(0, box.YMin - marginY);
        int cropXMax = Math.Min(imgW, box.XMax + marginX);
        int cropYMax = Math.Min(imgH, box.YMax + marginY);

        int cropW = cropXMax - cropXMin;
        int cropH = cropYMax - cropYMin;

        // Fallback if box is invalid
        if (cropW <= 0 || cropH <= 0)
            return originalImageBytes;

        // Crop
        using var cropped = image.Clone(ctx =>
            ctx.Crop(new Rectangle(cropXMin, cropYMin, cropW, cropH)));

        // Letterbox: resize maintaining aspect ratio, pad with black to 224x224
        const int targetSize = 224;
        float scale = Math.Min((float)targetSize / cropW, (float)targetSize / cropH);
        int newW = (int)(cropW * scale);
        int newH = (int)(cropH * scale);

        using var resized = cropped.Clone(ctx => ctx.Resize(newW, newH));

        // Create canvas with black background
        using var canvas = new Image<Rgb24>(targetSize, targetSize, new Rgb24(0, 0, 0));
        int offsetX = (targetSize - newW) / 2;
        int offsetY = (targetSize - newH) / 2;

        canvas.Mutate(ctx => ctx.DrawImage(resized, new Point(offsetX, offsetY), 1f));

        using var ms = new MemoryStream();
        canvas.SaveAsJpeg(ms);
        return ms.ToArray();
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

    private async Task<List<SnakeCandidateDto>> BuildOtherCandidateDtos(List<SpeciesPrediction> predictions)
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


    #endregion

    /// <inheritdoc />
    public async Task<IServiceResult> IdentifySnakeAsync(Stream? imageStream, string? contentType, long? fileSize)
    {
        if (imageStream == null || fileSize == null || fileSize <= 0 || string.IsNullOrEmpty(contentType))
        {
            return new ServiceResult(
                ResultCodeConst.SYS_Warning0008,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0008) // Invalid file
            );
        }

        var storageOpt = _storageOptions.Value;
        
        if (!IsAllowedImageType(contentType, storageOpt))
        {
            return new ServiceResult(
                ResultCodeConst.AI_Warning0004,
                await _msgService.GetMessageAsync(ResultCodeConst.AI_Warning0004)
            );
        }

        using var bufferStream = new MemoryStream();
        await imageStream.CopyToAsync(bufferStream);
        var imageBytes = bufferStream.ToArray();

        // Stage 1: YOLO Detection
        var detection = await _detectionService.DetectAsync(imageBytes);

        if (!detection.IsDetected || detection.Box == null)
        {
            return new ServiceResult(
                ResultCodeConst.AI_Success0001,
                AiInferenceConstants.MsgIdentifyDone,
                new SnakeIdentificationResponseDto
                {
                    Note = AiInferenceConstants.NoteIdentifyNotSnake
                }
            );
        }

        // Stage 1.5: Crop & Pad
        byte[] processedImageBytes;
        try
        {
            var yoloOpt = _yoloOptions.Value;
            processedImageBytes = CropAndPad(imageBytes, detection.Box, yoloOpt.MarginRatio);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to crop image. Using original image.");
            processedImageBytes = imageBytes;
        }

        // Stage 2: EfficientNetV2 Classification
        using var speciesStream = new MemoryStream(processedImageBytes);
        var speciesPredictions = await _classificationService.InferSpeciesOnlyAsync(speciesStream, topK: 3);
        var classificationPredictions = speciesPredictions.ToList();

        var top1 = classificationPredictions.FirstOrDefault();
        var maxConfidence = top1?.Confidence ?? 0;

        // Check for not_snake class
        if (top1 != null && string.Equals(top1.ClassName, NotSnakeClassName, StringComparison.OrdinalIgnoreCase))
        {
            return new ServiceResult(
                ResultCodeConst.AI_Success0001,
                AiInferenceConstants.MsgIdentifyDone,
                new SnakeIdentificationResponseDto
                {
                    Note = AiInferenceConstants.NoteIdentifyNotSnake
                }
            );
        }

        if (!classificationPredictions.Any() || maxConfidence < 0.50f)
        {
            return new ServiceResult(
                ResultCodeConst.AI_Success0001,
                AiInferenceConstants.MsgIdentifyDone,
                new SnakeIdentificationResponseDto
                {
                    Note = AiInferenceConstants.NoteIdentifyLowConf
                }
            );
        }

        var activeSnakes = await _unitOfWork.Repository<Snake, Guid>().GetAllAsync(tracked: false);
        var snakeDict = activeSnakes.Where(s => s.IsActive).ToDictionary(s => s.ScientificName, StringComparer.OrdinalIgnoreCase);

        Snake? FindSnakeLocal(string className)
        {
            var sciName = className.Replace("_", " ");
            return snakeDict.TryGetValue(sciName, out var s) ? s : null;
        }

        var response = new SnakeIdentificationResponseDto();
        var candidates = new List<SnakeCandidateDto>();
        IdentifiedSnakeDetailDto? primaryDetail = null;

        var first = true;
        foreach (var p in classificationPredictions)
        {
            var s = FindSnakeLocal(p.ClassName);
            if (s != null)
            {
                if (first)
                {
                    primaryDetail = new IdentifiedSnakeDetailDto
                    {
                        SnakeId = s.Id,
                        ScientificName = s.ScientificName,
                        CommonName = s.CommonName,
                        Confidence = Math.Round(p.Confidence, 4),
                        ToxicityLevel = s.ToxicityLevel,
                        ToxinGroup = s.ToxinGroup,
                        DangerSummary = GetDangerSummary(s.ToxicityLevel),
                        TypicalSymptoms = s.TypicalSymptoms,
                        Description = s.Description,
                        KeyIdentifiers = s.KeyIdentifiers,
                        Habitat = s.Habitat,
                        DistributionNote = s.DistributionNote,
                        Note = s.Note
                    };
                    first = false;
                }
                else
                {
                    candidates.Add(new SnakeCandidateDto
                    {
                        SnakeId = s.Id,
                        ScientificName = s.ScientificName,
                        CommonName = s.CommonName,
                        Confidence = Math.Round(p.Confidence, 4),
                        ToxicityLevel = s.ToxicityLevel,
                        ToxinGroup = s.ToxinGroup,
                        DangerSummary = GetDangerSummary(s.ToxicityLevel),
                        TypicalSymptoms = s.TypicalSymptoms
                    });
                }
            }
        }

        if (primaryDetail != null)
        {
            response.PrimarySnake = primaryDetail;
            response.OtherCandidates = candidates;
            response.Note = AiInferenceConstants.NoteIdentifyResult;
        }
        else
        {
            response.Note = AiInferenceConstants.NoteIdentifyNotFound;
        }

        string finalMessage;
        if (response.PrimarySnake != null)
        {
            finalMessage = string.Format(
                AiInferenceConstants.SuccessIdentifyFormat,
                response.PrimarySnake.CommonName,
                (response.PrimarySnake.Confidence * 100).ToString("0.##")
            );
        }
        else
        {
            finalMessage = AiInferenceConstants.MsgIdentifyDone;
        }

        return new ServiceResult(
            ResultCodeConst.AI_Success0001,
            finalMessage,
            response
        );
    }
}