using Hangfire;
using MapsterMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.AiInference;
using SFARS.Application.Dtos.AiReview;
using SFARS.Application.Dtos.Incident;
using SFARS.Application.Validations;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Common.Extensions;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.Params;
using SFARS.Infrastructure.Configurations;
using SFARS.Infrastructure.Helpers;
using SFARS.Infrastructure.Hubs;
using System.Text.Json;

namespace SFARS.Application.Services
{
    public class IncidentService : GenericService<Incident, IncidentDto, Guid>, IIncidentService<IncidentDto>
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IOptions<StorageOptions> _storageOptions;
        private readonly ISosSpamGuardService _spamGuard;
        private readonly IAiReviewService<SubmitAiReviewRequestDto, FirstAidStepDto> _aiReviewService;
        private readonly ISpeechToTextService _speechToTextService;
        private readonly IHubContext<RescueDispatchHub> _rescueHub;
        private readonly IHubContext<LocationTrackingHub> _locationHub;
        private readonly IFcmPushService _fcmService;
        private readonly IConfiguration _configuration;
        private readonly IDispatchService _dispatchService;

        public IncidentService(
            ISystemMessageService msgService,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<IncidentService> logger,
            IFileStorageService fileStorageService,
            IOptions<StorageOptions> storageOptions,
            ISosSpamGuardService spamGuard,
            IAiReviewService<SubmitAiReviewRequestDto, FirstAidStepDto> aiReviewService,
            ISpeechToTextService speechToTextService,
            IHubContext<RescueDispatchHub> rescueHub,
            IHubContext<LocationTrackingHub> locationHub,
            IFcmPushService fcmService,
            IConfiguration configuration,
            IDispatchService dispatchService)
            : base(msgService, unitOfWork, mapper, logger)
        {
            _fileStorageService = fileStorageService;
            _storageOptions = storageOptions;
            _spamGuard = spamGuard;
            _aiReviewService = aiReviewService;
            _speechToTextService = speechToTextService;
            _rescueHub = rescueHub;
            _locationHub = locationHub;
            _fcmService = fcmService;
            _configuration = configuration;
            _dispatchService = dispatchService;
        }


        /// <summary>
        /// Create a new incident (SOS report) with all related entities in single atomic transaction
        /// </summary>
        public async Task<IServiceResult> CreateIncidentAsync(Guid userId, IncidentDto dto)
        {
            if (userId == Guid.Empty)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013)
                );
            }

            var user = await _unitOfWork.Repository<User, Guid>().GetByIdAsync(userId);
            if (user == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0002,
                    string.Format(await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002), "User")
                );
            }

            // Anti-spam check
            var spamStatus = await _spamGuard.CheckAsync(userId);
            if (spamStatus == SosSpamStatus.HardBlocked)
            {
                return new ServiceResult(
                    ResultCodeConst.Incident_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0004)
                );
            }

            var validation = await ValidatorExtensions.ValidateAsync(dto);
            if (validation != null && !validation.IsValid)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001),
                    validation.ToProblemDetails().Errors
                );
            }

            // Cache system messages
            var reasonMsg = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Reason0001);
            var notifyTitle = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Notify0001);
            var notifyTemplate = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Notify0002);
            var successMsg = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Success0001);
            var failMsg = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001);

            var now = DateTime.UtcNow;

            // Create Point from lat/lng
            var location = new Point(dto.Longitude, dto.Latitude) { SRID = 4326 };

            // Create incident entity (with temporary code, required by validation/DB)
            var incident = new Incident
            {
                Id = Guid.NewGuid(),
                Code = "TEMP",
                VictimId = userId,
                Location = location,
                AddressString = dto.AddressString,
                Description = dto.Description,
                CurrentStatus = IncidentStatus.Pending,
                PriorityLevel = dto.PriorityLevel,
                // Auto-generate tracking code for public link/QR sharing (24h expiry)
                TrackingCode = LocationHelper.GenerateTrackingCode(),
                TrackingCodeExpiresAt = now.Add(LocationConstants.TrackingCodeExpiry),
                CreatedAt = now,
                CreatedBy = userId
            };

            // Create IncidentStatusHistory (audit log)
            var statusHistory = new IncidentStatusHistory
            {
                Id = Guid.NewGuid(),
                IncidentId = incident.Id,
                StatusFrom = null,
                StatusTo = IncidentStatus.Pending,
                ChangedBy = userId,
                ChangeReason = reasonMsg,
                CreatedAt = now,
                CreatedBy = userId
            };



            // Create NotificationLog for victim (confirmation)
            var notification = new NotificationLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = notifyTitle,
                Message = string.Empty,
                Type = NotificationType.Mission,
                IsRead = false,
                SentAt = now,
                CreatedAt = now,
                CreatedBy = userId
            };

            var sequenceValue = await _unitOfWork.GetNextSequenceValueAsync(SequenceNames.IncidentCode);
            var incidentCode = $"SOS-{now.Year}-{sequenceValue:D5}";

            incident.Code = incidentCode;
            notification.Message = string.Format(notifyTemplate, incidentCode);

            // Add all entities to repositories
            await _unitOfWork.Repository<Incident, Guid>().AddAsync(incident);
            await _unitOfWork.Repository<IncidentStatusHistory, Guid>().AddAsync(statusHistory);
            await _unitOfWork.Repository<NotificationLog, Guid>().AddAsync(notification);

            // Save - atomic transaction
            var saveResult = await _unitOfWork.SaveChangesAsync();

            if (saveResult > 0)
            {
                var resultDto = _mapper.Map<IncidentDto>(incident);
                resultDto.VictimName = user.FullName;
                resultDto.Latitude  = dto.Latitude;
                resultDto.Longitude = dto.Longitude;

                return new ServiceResult(
                    ResultCodeConst.Incident_Success0001,
                    successMsg,
                    resultDto
                );
            }
            return new ServiceResult(ResultCodeConst.SYS_Fail0001, failMsg);
        }

        /// <summary>
        /// Get incidents reported by the current user with standard pagination
        /// </summary>
        public async Task<IServiceResult> GetMyIncidentsAsync(Guid userId, IncidentSpecParams specParams)
        {
            if (userId == Guid.Empty)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013)
                );
            }

            var countSpec = new IncidentSpecification(specParams, userId, isCount: true);
            var totalItems = await _unitOfWork.Repository<Incident, Guid>().CountAsync(countSpec);

            var spec = new IncidentSpecification(specParams, userId, isCount: false);

            var dtos = await _unitOfWork.Repository<Incident, Guid>()
                .GetAllWithSpecAndSelectorAsync(spec, i => new IncidentHistoryDto
                {
                    Id = i.Id,
                    Code = i.Code,
                    CurrentStatus = i.CurrentStatus,
                    PriorityLevel = i.PriorityLevel,
                    Latitude = i.Location.Y,
                    Longitude = i.Location.X,
                    AddressString = i.AddressString,
                    IsVerified = i.CurrentAiReview != null && i.CurrentAiReview.AdminReviewerId != null,

                    CreatedAt = i.CreatedAt,
                    IncidentImage = i.Medias.OrderBy(m => m.MediaType).Select(m => m.MediaUrl).FirstOrDefault()
                }, tracked: false);

            var limit = specParams.GetTake();
            var page = specParams.GetPage();
            var totalPages = limit > 0 ? (int)Math.Ceiling(totalItems / (double)limit) : 0;

            var pagedResult = new PaginatedResultDto<IncidentHistoryDto>(
                dtos,
                page,
                limit,
                totalPages,
                totalItems
            );

            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                pagedResult
            );
        }

        public async Task<IServiceResult> GetAllIncidentsAsync(IncidentSpecParams specParams)
        {
            var countSpec = new IncidentSpecification(specParams, null, isCount: true);
            var totalItems = await _unitOfWork.Repository<Incident, Guid>().CountAsync(countSpec);

            var spec = new IncidentSpecification(specParams, null, isCount: false);

            var dtos = await _unitOfWork.Repository<Incident, Guid>()
                .GetAllWithSpecAndSelectorAsync(spec, i => new IncidentDto
                {
                    Id = i.Id,
                    Code = i.Code,
                    Latitude = i.Location.Y,
                    Longitude = i.Location.X,
                    AddressString = i.AddressString,
                    Description = i.Description,
                    CurrentStatus = i.CurrentStatus,
                    PriorityLevel = i.PriorityLevel,
                    AiConfidenceScore = i.AiConfidenceScore,
                    IncidentImage = i.Medias.OrderBy(m => m.MediaType).Select(m => m.MediaUrl).FirstOrDefault(),
                    IsVerified = i.CurrentAiReview != null && i.CurrentAiReview.AdminReviewerId != null,
                    CurrentAiReviewStatus = i.CurrentAiReviewStatus,
                    VictimId = i.VictimId,
                    VictimName = i.Victim.FullName,
                    CreatedAt = i.CreatedAt
                }, tracked: false);

            var limit = specParams.GetTake();
            var page = specParams.GetPage();
            var totalPages = limit > 0 ? (int)Math.Ceiling(totalItems / (double)limit) : 0;

            var pagedResult = new PaginatedResultDto<IncidentDto>(
                dtos,
                page,
                limit,
                totalPages,
                totalItems
            );

            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                pagedResult
            );
        }

        public async Task<IServiceResult> GetStatusHistoryAsync(Guid incidentId)
        {
            var histories = await _unitOfWork.Repository<IncidentStatusHistory, Guid>()
                .GetQueryable(tracked: false)
                .Where(h => h.IncidentId == incidentId)
                .ToListAsync();

            // Fetch users to map ChangedByName
            var userIds = histories.Select(h => h.ChangedBy).Distinct();
            var users = await _unitOfWork.Repository<User, Guid>()
                .GetQueryable(tracked: false)
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();
            var userDict = users.ToDictionary(u => u.Id, u => u.FullName);

            var dtos = histories.OrderByDescending(h => h.CreatedAt).Select(h => new IncidentStatusHistoryDto
            {
                Id = h.Id,
                IncidentId = h.IncidentId,
                StatusFrom = h.StatusFrom,
                StatusTo = h.StatusTo,
                ChangeReason = h.ChangeReason,
                CreatedAt = h.CreatedAt,
                ChangedBy = h.ChangedBy,
                ChangedByName = userDict.GetValueOrDefault(h.ChangedBy, "Unknown")
            }).ToList();

            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                dtos
            );
        }

        public async Task<IServiceResult> GetRecentCommunityIncidentsAsync(BaseSpecParams specParams)
        {
            var countSpec = new CommunityIncidentSpecification(specParams, isCount: true);
            var totalItems = await _unitOfWork.Repository<Incident, Guid>().CountAsync(countSpec);

            var spec = new CommunityIncidentSpecification(specParams, isCount: false);

            var dtos = await _unitOfWork.Repository<Incident, Guid>()
                .GetAllWithSpecAndSelectorAsync(spec, i => new CommunityIncidentDto
                {
                    Id = i.Id,
                    Code = i.Code,
                    // Round to 3 decimal places (~110m resolution) for privacy
                    Latitude = Math.Round(i.Location.Y, 3),
                    Longitude = Math.Round(i.Location.X, 3),
                    AddressString = i.AddressString,
                    CurrentStatus = i.CurrentStatus,
                    IsVerified = i.CurrentAiReview != null && i.CurrentAiReview.AdminReviewerId != null,
                    PriorityLevel = i.PriorityLevel,
                    CreatedAt = i.CreatedAt,
                    IncidentImage = i.Medias.OrderBy(m => m.MediaType).Select(m => m.MediaUrl).FirstOrDefault()
                }, tracked: false);

            var limit = specParams.GetTake();
            var page = specParams.GetPage();
            var totalPages = limit > 0 ? (int)Math.Ceiling(totalItems / (double)limit) : 0;

            var pagedResult = new PaginatedResultDto<CommunityIncidentDto>(
                dtos,
                page,
                limit,
                totalPages,
                totalItems
            );

            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                pagedResult
            );
        }

        /// <summary>
        /// Get incident by ID with authorization check (user must be victim or rescuer)
        /// </summary>
        public async Task<IServiceResult> GetIncidentByIdAsync(Guid incidentId)
        {
            var spec = new BaseSpecification<Incident>(i => i.Id == incidentId);
            spec.ApplyInclude(q => q.Include(i => i.Victim));
            spec.ApplyInclude(q => q.Include(i => i.Medias));
            spec.ApplyInclude(q => q.Include(i => i.Missions));
            spec.ApplyInclude(q => q.Include(i => i.CurrentAiInference)
                                     .ThenInclude(ai => ai.Candidates)
                                     .ThenInclude(c => c.Snake));

            var incident = await _unitOfWork.Repository<Incident, Guid>()
                .GetWithSpecAsync(spec, tracked: false);

            if (incident == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004)
                );
            }

            var activeMission = incident.Missions
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault(m => m.Status == RescueStatus.Accepted || 
                                     m.Status == RescueStatus.Arrived ||
                                     m.Status == RescueStatus.Completed);

            LocationCoords? rescuerLoc = null;
            if (activeMission != null && (activeMission.Status == RescueStatus.Accepted || activeMission.Status == RescueStatus.Arrived || activeMission.Status == RescueStatus.Completed))
            {
                var firstLog = await _unitOfWork.Repository<RescueTrackingLog, long>()
                    .GetQueryable(tracked: false)
                    .Where(t => t.MissionId == activeMission.Id)
                    .OrderBy(t => t.LoggedAt)
                    .FirstOrDefaultAsync();

                if (firstLog != null)
                {
                    rescuerLoc = new LocationCoords 
                    { 
                        Latitude = firstLog.Location.Y, 
                        Longitude = firstLog.Location.X 
                    };
                }
            }

            var dto = new IncidentDetailDto
            {
                Id = incident.Id,
                Code = incident.Code,
                Patient = new LocationCoords { Latitude = incident.Location.Y, Longitude = incident.Location.X },
                Rescuer = rescuerLoc,
                AddressString = incident.AddressString,
                Description = incident.Description,
                CurrentStatus = incident.CurrentStatus,
                PriorityLevel = incident.PriorityLevel,
                AiConfidenceScore = incident.AiConfidenceScore,
                IncidentImage = incident.Medias.OrderBy(m => m.MediaType).Select(m => m.MediaUrl).FirstOrDefault(),
                CreatedAt = incident.CreatedAt,
                RescuerId = activeMission?.RescuerId
            };

            // Map AI Results if available
            if (incident.CurrentAiInference != null)
            {
                // Check if Human (Admin or Rescuer) has overridden the result
                bool isHumanCorrected = false;
                Guid? effectiveSnakeId = null;
                bool? effectiveIsSnakeBite = null;

                var review = await _unitOfWork.Repository<AiInferenceReview, Guid>()
                    .GetQueryable(tracked: false)
                    .FirstOrDefaultAsync(r => r.AiInferenceId == incident.CurrentAiInference.Id);

                if (review?.AdminReviewerId != null)
                {
                    dto.IsVerified = true;
                    // Admin reviewed: use admin's finalized data if they corrected or approved a rescuer correction
                    if (review.CorrectedSnakeId.HasValue)
                    {
                        isHumanCorrected = true;
                        effectiveSnakeId = review.CorrectedSnakeId;
                    }
                    if (review.IsConfirmedWoundSnakeBite.HasValue)
                    {
                        effectiveIsSnakeBite = review.IsConfirmedWoundSnakeBite;
                    }
                }

                // 1. Wound Analysis
                var isWoundBite = isHumanCorrected && effectiveIsSnakeBite.HasValue 
                                  ? effectiveIsSnakeBite.Value 
                                  : incident.CurrentAiInference.IsSnakeBite;

                if (isWoundBite.HasValue)
                {
                    dto.WoundAnalysis = new WoundAnalysisDto
                    {
                        IsWoundDetected = true,
                        IsSnakeBite = isWoundBite.Value,
                        Confidence = isHumanCorrected ? 1.0f : (incident.AiConfidenceScore ?? 0)
                    };
                }

                // 2. Snake Predictions
                if (isHumanCorrected && effectiveSnakeId.HasValue)
                {
                    var overridingSnake = await _unitOfWork.Repository<Snake, Guid>().GetByIdAsync(effectiveSnakeId.Value);
                    if (overridingSnake != null)
                    {
                        dto.PrimarySnake = new SnakeCandidateDto
                        {
                            SnakeId = overridingSnake.Id,
                            ScientificName = overridingSnake.ScientificName,
                            CommonName = overridingSnake.CommonName,
                            Confidence = 1.0f,
                            ToxicityLevel = overridingSnake.ToxicityLevel,
                            ToxinGroup = overridingSnake.ToxinGroup,
                            DangerSummary = AiInferenceConstants.GetDangerLabel(overridingSnake.ToxicityLevel),
                            TypicalSymptoms = overridingSnake.TypicalSymptoms
                        };
                        dto.OtherCandidates = new List<SnakeCandidateDto>(); // Clear others if human overrode
                    }
                }
                else
                {
                    var allPredictions = incident.CurrentAiInference.Candidates
                        .OrderBy(c => c.Rank)
                        .Select(c => new SnakeCandidateDto
                        {
                            SnakeId = c.SnakeId,
                            ScientificName = c.Snake.ScientificName,
                            CommonName = c.Snake.CommonName,
                            Confidence = c.Confidence,
                            ToxicityLevel = c.Snake.ToxicityLevel,
                            ToxinGroup = c.Snake.ToxinGroup,
                            DangerSummary = AiInferenceConstants.GetDangerLabel(c.Snake.ToxicityLevel),
                            TypicalSymptoms = c.Snake.TypicalSymptoms
                        }).ToList();

                    dto.PrimarySnake = allPredictions.FirstOrDefault();
                    dto.OtherCandidates = allPredictions.Skip(dto.PrimarySnake != null ? 1 : 0).ToList();
                }
            }

            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                dto
            );
        }

        public async Task<IServiceResult> GetAdminIncidentDetailAsync(Guid incidentId)
        {
            var spec = new BaseSpecification<Incident>(i => i.Id == incidentId);
            spec.ApplyInclude(q => q.Include(i => i.Victim));
            spec.ApplyInclude(q => q.Include(i => i.Medias));
            spec.ApplyInclude(q => q.Include(i => i.Missions));
            spec.ApplyInclude(q => q.Include(i => i.CurrentAiInference!)
                                     .ThenInclude(ai => ai.Candidates)
                                     .ThenInclude(c => c.Snake!));
            spec.ApplyInclude(q => q.Include(i => i.CurrentAiReview!)
                                     .ThenInclude(r => r.Reviewer!));
            spec.ApplyInclude(q => q.Include(i => i.CurrentAiReview!)
                                     .ThenInclude(r => r.AdminReviewer!));
            spec.ApplyInclude(q => q.Include(i => i.CurrentAiReview!)
                                     .ThenInclude(r => r.CorrectedSnake!));

            var incident = await _unitOfWork.Repository<Incident, Guid>()
                .GetWithSpecAsync(spec, tracked: false);

            if (incident == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004)
                );
            }

            var activeMission = incident.Missions
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault(m => m.Status == RescueStatus.Accepted || 
                                     m.Status == RescueStatus.Arrived ||
                                     m.Status == RescueStatus.Completed);

            LocationCoords? rescuerLoc = null;
            if (activeMission != null && (activeMission.Status == RescueStatus.Accepted || activeMission.Status == RescueStatus.Arrived || activeMission.Status == RescueStatus.Completed))
            {
                var firstLog = await _unitOfWork.Repository<RescueTrackingLog, long>()
                    .GetQueryable(tracked: false)
                    .Where(t => t.MissionId == activeMission.Id)
                    .OrderBy(t => t.LoggedAt)
                    .FirstOrDefaultAsync();

                if (firstLog != null)
                {
                    rescuerLoc = new LocationCoords 
                    { 
                        Latitude = firstLog.Location.Y, 
                        Longitude = firstLog.Location.X 
                    };
                }
            }

            var dto = new AdminIncidentDetailDto
            {
                Id = incident.Id,
                Code = incident.Code,
                Patient = new LocationCoords { Latitude = incident.Location.Y, Longitude = incident.Location.X },
                Rescuer = rescuerLoc,
                AddressString = incident.AddressString,
                Description = incident.Description,
                CurrentStatus = incident.CurrentStatus,
                PriorityLevel = incident.PriorityLevel,
                IncidentImage = incident.Medias.OrderBy(m => m.MediaType).Select(m => m.MediaUrl).FirstOrDefault(),
                CreatedAt = incident.CreatedAt,
                RescuerId = activeMission?.RescuerId
            };

            if (incident.CurrentAiInference != null)
            {
                var aiCandidates = incident.CurrentAiInference.Candidates
                    .OrderBy(c => c.Rank)
                    .Select(c => new SnakeCandidateDto
                    {
                        SnakeId = c.SnakeId,
                        ScientificName = c.Snake.ScientificName,
                        CommonName = c.Snake.CommonName,
                        Confidence = c.Confidence,
                        ToxicityLevel = c.Snake.ToxicityLevel,
                        ToxinGroup = c.Snake.ToxinGroup,
                        DangerSummary = AiInferenceConstants.GetDangerLabel(c.Snake.ToxicityLevel),
                        TypicalSymptoms = c.Snake.TypicalSymptoms
                    }).ToList();

                var aiWoundAnalysis = incident.CurrentAiInference.IsSnakeBite.HasValue ? new WoundAnalysisDto
                {
                    IsWoundDetected = true,
                    IsSnakeBite = incident.CurrentAiInference.IsSnakeBite.Value,
                    Confidence = incident.AiConfidenceScore ?? 0
                } : null;

                dto.OriginalAiPrediction = new OriginalAiPredictionDto
                {
                    AiInferenceId = incident.CurrentAiInference.Id,
                    AiConfidenceScore = incident.AiConfidenceScore,
                    WoundAnalysis = aiWoundAnalysis,
                    PrimarySnake = aiCandidates.FirstOrDefault(),
                    OtherCandidates = aiCandidates.Skip(1).ToList()
                };
            }

            if (incident.CurrentAiReview != null)
            {
                var review = incident.CurrentAiReview;
                
                string? rescuerSnakeName = null;
                if (incident.HumanReviewedSnakeId.HasValue)
                {
                    var rescuerSnake = await _unitOfWork.Repository<Snake, Guid>().GetByIdAsync(incident.HumanReviewedSnakeId.Value);
                    rescuerSnakeName = rescuerSnake?.CommonName ?? rescuerSnake?.ScientificName;
                }
                
                dto.RescuerReviewData = new RescuerReviewDataDto
                {
                    ReviewerId = review.ReviewerId,
                    ReviewerName = review.Reviewer?.FullName,
                    ReviewStatus = incident.CurrentAiReviewStatus ?? AiReviewStatus.Pending,
                    CorrectedSnakeId = incident.HumanReviewedSnakeId,
                    CorrectedSnakeName = rescuerSnakeName,
                    CorrectedToxinGroup = incident.HumanReviewedToxinGroup,
                    IsConfirmedWoundSnakeBite = incident.HumanConfirmedSnakeBite,
                    UnableToAssessReasonChoice = null, // Rescuer doesn't save unable reason in snapshot currently, wait it doesn't? Actually Rescuer might, but we can just use the review if it wasn't admin overwritten. But for now we just use review's if admin is null.
                    RescuerComment = review.Comment,
                    ReviewedAt = review.ReviewedAt
                };
                
                // If the admin hasn't reviewed yet, then the review record actually still holds the rescuer's reason choice.
                if (review.AdminReviewerId == null) 
                {
                    dto.RescuerReviewData.UnableToAssessReasonChoice = review.UnableToAssessReasonChoice;
                }

                if (review.AdminReviewerId != null)
                {
                    dto.AdminReviewData = new AdminReviewDataDto
                    {
                        AdminReviewerId = review.AdminReviewerId.Value,
                        AdminReviewerName = review.AdminReviewer?.FullName,
                        ReviewStatus = review.ReviewStatus,
                        CorrectedSnakeId = review.CorrectedSnakeId,
                        CorrectedSnakeName = review.CorrectedSnake?.CommonName ?? review.CorrectedSnake?.ScientificName,
                        CorrectedToxinGroup = review.CorrectedToxinGroup,
                        IsConfirmedWoundSnakeBite = review.IsConfirmedWoundSnakeBite,
                        UnableToAssessReasonChoice = review.UnableToAssessReasonChoice,
                        AdminComment = review.AdminComment,
                        AdminReviewedAt = review.UpdatedAt ?? review.ReviewedAt
                    };
                    
                    // In Admin case, if Rescuer had an unable to assess reason, it might be overwritten. So we just leave rescuer's as null or copy it if status was UnableToAssess. (Logic separation).
                }
            }

            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                dto
            );
        }

        /// <summary>
        /// Upload media (photo/video) for an incident
        /// </summary>
        public async Task<IServiceResult> UploadMediaAsync(
            Guid userId,
            Guid incidentId,
            Stream stream,
            string fileName,
            string contentType,
            long fileSize,
            MediaType mediaType)
        {
            // Validate inputs
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

            // Get incident and validate ownership
            var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);
            if (incident == null)
            {
                return new ServiceResult(
                    ResultCodeConst.Incident_Warning0002,
                    string.Format(await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0002), "Incident")
                );
            }

            // Authorization: Only victim can upload media
            if (incident.VictimId != userId)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0007,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0007)
                );
            }

            // Business rule: Cannot upload if incident is closed or cancelled
            if (incident.CurrentStatus == IncidentStatus.Closed ||
                incident.CurrentStatus == IncidentStatus.Cancelled)
            {
                return new ServiceResult(
                    ResultCodeConst.Incident_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0001)
                );
            }

            // Validate file size
            var storageOpt = _storageOptions.Value;
            if (fileSize <= 0)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001)
                );
            }

            if (fileSize > storageOpt.MaxUploadBytes)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0008,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0008)
                );
            }

            // Validate content type
            if (!IsAllowedContentType(contentType, mediaType, storageOpt))
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001)
                );
            }

            try
            {
                // Upload to cloud storage (folder from config)
                var folder = string.Format(storageOpt.IncidentMediaFolderFormat, incidentId);
                var uploadResult = await _fileStorageService.UploadAsync(stream, fileName, folder, contentType);

                // Create media record
                var now = DateTime.UtcNow;
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
                var saveResult = await _unitOfWork.SaveChangesAsync();

                if (saveResult > 0)
                {
                    var dto = new IncidentMediaDto
                    {
                        Id = media.Id,
                        IncidentId = media.IncidentId,
                        MediaUrl = media.MediaUrl,
                        MediaType = media.MediaType,
                        CreatedAt = media.CreatedAt
                    };

                    return new ServiceResult(
                        ResultCodeConst.SYS_Success0001,
                        await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001),
                        dto
                    );
                }

                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001)
                );
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("FileStorage:"))
            {
                // Expected: Cloud storage failure
                _logger.LogWarning(ex, "File upload failed for incident {IncidentId}", incidentId);
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001)
                );
            }
            catch (DbUpdateException ex)
            {
                // Expected: Database save failure
                _logger.LogError(ex, "Database save failed for incident media. IncidentId={IncidentId}", incidentId);
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001)
                );
            }
        }

        /// <summary>
        /// Validate if content type is allowed for the given media type
        /// </summary>
        private static bool IsAllowedContentType(string contentType, MediaType mediaType, StorageOptions options)
        {
            if (string.IsNullOrWhiteSpace(contentType))
                return false;

            var isImage = options.AllowedImageTypes.Any(t => 
                contentType.Equals(t, StringComparison.OrdinalIgnoreCase));
            var isVideo = options.AllowedVideoTypes.Any(t => 
                contentType.Equals(t, StringComparison.OrdinalIgnoreCase));

            return mediaType switch
            {
                MediaType.SnakePhoto => isImage,
                MediaType.BiteWoundPhoto => isImage,
                MediaType.Other => isImage || isVideo,
                _ => false
            };
        }

        #region Tracking

        /// <summary>
        /// Get full tracking data for an incident (authenticated participants only).
        /// Uses batch user query to avoid N+1.
        /// </summary>
        public async Task<IServiceResult> GetIncidentTrackingAsync(Guid userId, Guid incidentId)
        {
            if (userId == Guid.Empty)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013));
            }

            // Get incident with missions (authorization check embedded)
            var spec = new BaseSpecification<Incident>(i =>
                i.Id == incidentId &&
                (i.VictimId == userId ||
                 i.Missions.Any(m => m.RescuerId == userId)));
            spec.ApplyInclude(q => q.Include(i => i.Missions));

            var incident = await _unitOfWork.Repository<Incident, Guid>()
                .GetWithSpecAsync(spec, tracked: false);

            if (incident == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
            }

            // Batch query all participant user IDs
            var participantIds = new List<Guid> { incident.VictimId };
            participantIds.AddRange(incident.Missions
                .Where(m => m.Status == RescueStatus.Accepted || m.Status == RescueStatus.Arrived)
                .Select(m => m.RescuerId));

            var users = await _unitOfWork.Repository<User, Guid>()
                .GetAllWithSpecAsync(new BaseSpecification<User>(u => participantIds.Contains(u.Id)),
                    tracked: false);

            var participants = BuildParticipants(users, incident.VictimId);

            var dto = new IncidentTrackingDto
            {
                IncidentId = incident.Id,
                IncidentCode = incident.Code,
                TrackingCode = incident.TrackingCode,
                Status = incident.CurrentStatus,
                IncidentLatitude = incident.Location.Y,
                IncidentLongitude = incident.Location.X,
                Participants = participants
            };

            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                dto);
        }

        /// <summary>
        /// Get incident tracking data via public tracking code (no auth).
        /// Returns minimized DTO without userId/userName.
        /// </summary>
        public async Task<IServiceResult> GetPublicTrackingAsync(string trackingCode)
        {
            if (string.IsNullOrWhiteSpace(trackingCode))
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));
            }

            var spec = new BaseSpecification<Incident>(i =>
                i.TrackingCode == trackingCode
                && i.TrackingCodeExpiresAt != null
                && i.TrackingCodeExpiresAt > DateTime.UtcNow
                && i.CurrentStatus != IncidentStatus.Closed
                && i.CurrentStatus != IncidentStatus.Cancelled);
            spec.ApplyInclude(q => q.Include(i => i.Missions));

            var incident = await _unitOfWork.Repository<Incident, Guid>()
                .GetWithSpecAsync(spec, tracked: false);

            if (incident == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0002,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));
            }

            // Batch query all participant users
            var participantIds = new List<Guid> { incident.VictimId };
            participantIds.AddRange(incident.Missions
                .Where(m => m.Status == RescueStatus.Accepted || m.Status == RescueStatus.Arrived)
                .Select(m => m.RescuerId));

            var users = await _unitOfWork.Repository<User, Guid>()
                .GetAllWithSpecAsync(new BaseSpecification<User>(u => participantIds.Contains(u.Id)),
                    tracked: false);

            var publicParticipants = users.Select(u => new PublicTrackingParticipantDto
            {
                Role = u.Id == incident.VictimId ? "User" : "Rescuer",
                Latitude = u.CurrentLocation?.Y,
                Longitude = u.CurrentLocation?.X,
                LocationUpdatedAt = u.LocationUpdatedAt,
                AccuracyLevel = LocationHelper.GetAccuracyLevel(u.LocationAccuracyMeters)
            }).ToList();

            var dto = new PublicIncidentTrackingDto
            {
                IncidentCode = incident.Code,
                Status = incident.CurrentStatus.ToString(),
                IncidentLatitude = incident.Location.Y,
                IncidentLongitude = incident.Location.X,
                Participants = publicParticipants
            };

            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                dto);
        }

        /// <summary>
        /// Generate or regenerate a tracking code for an incident (victim only).
        /// Code expires in 24 hours.
        /// </summary>
        public async Task<IServiceResult> RegenerateTrackingCodeAsync(Guid userId, Guid incidentId)
        {
            if (userId == Guid.Empty)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013));
            }

            var incident = await _unitOfWork.Repository<Incident, Guid>()
                .GetWithSpecAsync(new BaseSpecification<Incident>(i =>
                    i.Id == incidentId && i.VictimId == userId));

            if (incident == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
            }

            if (!incident.CurrentStatus.IsTrackable())
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));
            }

            incident.TrackingCode = LocationHelper.GenerateTrackingCode();
            incident.TrackingCodeExpiresAt = DateTime.UtcNow.Add(LocationConstants.TrackingCodeExpiry);

            await _unitOfWork.Repository<Incident, Guid>().UpdateAsync(incident);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult(
                ResultCodeConst.SYS_Success0003,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0003),
                new
                {
                    TrackingCode = incident.TrackingCode,
                    ExpiresAt = incident.TrackingCodeExpiresAt
                });
        }

        /// <summary>
        /// Build participant list from User entities (shared by auth and public endpoints).
        /// </summary>
        private static List<TrackingParticipantDto> BuildParticipants(
            IEnumerable<User> users, Guid victimId)
        {
            return users.Select(u => new TrackingParticipantDto
            {
                UserId = u.Id,
                UserName = u.FullName ?? u.Email ?? "Unknown",
                Role = u.Id == victimId ? "User" : "Rescuer",
                Latitude = u.CurrentLocation?.Y,
                Longitude = u.CurrentLocation?.X,
                LocationUpdatedAt = u.LocationUpdatedAt,
                AccuracyMeters = u.LocationAccuracyMeters,
                AccuracyLevel = LocationHelper.GetAccuracyLevel(u.LocationAccuracyMeters)
            }).ToList();
        }

        #endregion

        #region Grace Period Cancel

        /// <inheritdoc />
        public async Task<IServiceResult> CancelIncidentAsync(Guid userId, Guid incidentId)
        {
            // Load incident and verify ownership
            var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);

            if (incident == null)
                return new ServiceResult(
                    ResultCodeConst.Incident_Warning0002,
                    string.Format(await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0002), "Incident")
                );

            if (incident.VictimId != userId)
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013)
                );

            // Only Pending incidents can be cancelled in grace period
            if (incident.CurrentStatus != IncidentStatus.Pending)
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004)
                );

            // Validate against server-authoritative grace period
            if (incident.GraceExpiresAt == null)
                return new ServiceResult(
                    ResultCodeConst.Incident_Warning0005,
                    await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0005)
                );

            if (DateTime.UtcNow > incident.GraceExpiresAt.Value)
                return new ServiceResult(
                    ResultCodeConst.Incident_Warning0006,
                    await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0006)
                );

            // Transition to Cancelled + audit
            var now = DateTime.UtcNow;
            incident.CurrentStatus = IncidentStatus.Cancelled;
            incident.UpdatedAt = now;
            incident.UpdatedBy = userId;

            var statusHistory = new IncidentStatusHistory
            {
                Id = Guid.NewGuid(),
                IncidentId = incidentId,
                StatusFrom = IncidentStatus.Pending,
                StatusTo = IncidentStatus.Cancelled,
                ChangedBy = userId,
                ChangeReason = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Reason0002),
                CreatedAt = now,
                CreatedBy = userId
            };

            // Cancel pending Hangfire dispatch jobs to prevent orphan dispatch after victim cancels
            if (!string.IsNullOrWhiteSpace(incident.DispatchJobIds))
            {
                var jobIds = JsonSerializer.Deserialize<string[]>(incident.DispatchJobIds);
                if (jobIds != null)
                    foreach (var jobId in jobIds)
                        BackgroundJob.Delete(jobId);

                incident.DispatchJobIds = null;
            }

            await _unitOfWork.Repository<Incident, Guid>().UpdateAsync(incident);
            await _unitOfWork.Repository<IncidentStatusHistory, Guid>().AddAsync(statusHistory);

            var saved = await _unitOfWork.SaveChangesAsync();
            if (saved <= 0)
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001)
                );

            // Real-time: Notify community that this incident was cancelled
            await _dispatchService.NotifyCommunityAsync(incidentId);

            // Record cancellation in anti-spam guard (fire-and-forget)
            await _spamGuard.RecordCancellationAsync(userId);

            _logger.LogInformation(
                "Incident {IncidentId} cancelled by victim {UserId} within grace period.",
                incidentId, userId);

            return new ServiceResult(
                ResultCodeConst.Incident_Success0005,
                await _msgService.GetMessageAsync(ResultCodeConst.Incident_Success0005)
            );
        }

        /// <inheritdoc />
        public async Task<IServiceResult> ManualDispatchAsync(Guid userId, Guid incidentId)
        {
            var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);

            if (incident == null)
                return new ServiceResult(
                    ResultCodeConst.Incident_Warning0002,
                    string.Format(await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0002), "Incident")
                );

            if (incident.VictimId != userId)
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013)
                );

            if (incident.CurrentStatus != IncidentStatus.Pending && incident.CurrentStatus != IncidentStatus.Cancelled)
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004)
                );

            var now = DateTime.UtcNow;

            if (incident.CurrentStatus == IncidentStatus.Cancelled)
            {
                incident.CurrentStatus = IncidentStatus.Pending;
                incident.UpdatedAt = now;
                incident.UpdatedBy = userId;

                var statusHistory = new IncidentStatusHistory
                {
                    Id = Guid.NewGuid(),
                    IncidentId = incidentId,
                    StatusFrom = IncidentStatus.Cancelled,
                    StatusTo = IncidentStatus.Pending,
                    ChangedBy = userId,
                    ChangeReason = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Reason0009),
                    CreatedAt = now,
                    CreatedBy = userId
                };

                await _unitOfWork.Repository<Incident, Guid>().UpdateAsync(incident);
                await _unitOfWork.Repository<IncidentStatusHistory, Guid>().AddAsync(statusHistory);

                var saved = await _unitOfWork.SaveChangesAsync();
                if (saved <= 0)
                    return new ServiceResult(
                        ResultCodeConst.SYS_Fail0001,
                        await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001)
                    );
            }

            // Trigger dispatch immediately
            BackgroundJob.Enqueue<IDispatchService>(s => s.StartDispatchAsync(incidentId));

            _logger.LogInformation(
                "Manual dispatch triggered for incident {IncidentId} by victim {UserId}.",
                incidentId, userId);

            return new ServiceResult(
                ResultCodeConst.Incident_Success0001, // Reusing creation success or generic success
                await _msgService.GetMessageAsync(ResultCodeConst.Incident_Success0001)
            );
        }

        /// <inheritdoc />
        public async Task<IServiceResult> ResolveFallbackAsync(Guid userId, Guid incidentId)
        {
            // Load incident and verify ownership
            var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);

            if (incident == null)
                return new ServiceResult(
                    ResultCodeConst.Incident_Warning0002,
                    string.Format(await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0002), "Incident")
                );

            if (incident.VictimId != userId)
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013)
                );

            // Only Unassigned incidents can be resolved by victim via fallback
            if (incident.CurrentStatus != IncidentStatus.Unassigned)
                return new ServiceResult(
                    ResultCodeConst.Incident_Warning0009,
                    await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0009)
                );

            // Transition to Closed + audit
            var now = DateTime.UtcNow;
            incident.CurrentStatus = IncidentStatus.Closed;
            incident.UpdatedAt = now;
            incident.UpdatedBy = userId;

            var statusHistory = new IncidentStatusHistory
            {
                Id = Guid.NewGuid(),
                IncidentId = incidentId,
                StatusFrom = IncidentStatus.Unassigned,
                StatusTo = IncidentStatus.Closed,
                ChangedBy = userId,
                ChangeReason = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Reason0008),
                CreatedAt = now,
                CreatedBy = userId
            };

            // Cancel any pending Hangfire dispatch jobs
            if (!string.IsNullOrWhiteSpace(incident.DispatchJobIds))
            {
                var jobIds = JsonSerializer.Deserialize<string[]>(incident.DispatchJobIds);
                if (jobIds != null)
                    foreach (var jobId in jobIds)
                        BackgroundJob.Delete(jobId);

                incident.DispatchJobIds = null;
            }

            await _unitOfWork.Repository<Incident, Guid>().UpdateAsync(incident);
            await _unitOfWork.Repository<IncidentStatusHistory, Guid>().AddAsync(statusHistory);

            var saved = await _unitOfWork.SaveChangesAsync();
            if (saved <= 0)
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001)
                );

            // Real-time: Notify community that this incident is resolved/closed
            await _dispatchService.NotifyCommunityAsync(incidentId);

            _logger.LogInformation(
                "Incident {IncidentId} resolved via fallback by victim {UserId}.",
                incidentId, userId);

            return new ServiceResult(
                ResultCodeConst.Incident_Success0006,
                await _msgService.GetMessageAsync(ResultCodeConst.Incident_Success0006)
            );
        }

        /// <inheritdoc />
        public async Task<IServiceResult> UpdateVoiceSymptomAsync(
            Guid userId, 
            Guid incidentId, 
            Stream audioStream, 
            string fileName, 
            string contentType)
        {
            if (audioStream == null || audioStream.Length == 0)
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0008,
                    string.Format(await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0008), "Audio file")
                );

            var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);

            if (incident == null)
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0002,
                    string.Format(await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002), "Incident")
                );

            if (incident.VictimId != userId)
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013)
                );

            using var readStream = new MemoryStream();
            await audioStream.CopyToAsync(readStream);
            var audioBytes = readStream.ToArray();

            // Storage Upload (takes ownership of its stream wrapper and disposes it)
            using var storageStream = new MemoryStream(audioBytes);
            var uploadResult = await _fileStorageService.UploadAsync(
                storageStream,
                fileName,
                $"incidents/{incidentId}/symptoms",
                contentType
            );

            if (uploadResult == null || string.IsNullOrEmpty(uploadResult.Url))
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001)
                );
            }

            // AI STT Extraction (takes ownership of its stream wrapper)
            using var sttStream = new MemoryStream(audioBytes);
            var extractionResult = await _speechToTextService.TranscribeAndExtractAsync(sttStream, fileName, contentType);

            incident.SymptomAudioUrl = uploadResult.Url;
            if (extractionResult != null)
            {
                incident.SymptomText = extractionResult.Transcript;
                incident.MinutesSinceBite = extractionResult.MinutesSinceBite;
                
                if (extractionResult.Symptoms != null && extractionResult.Symptoms.Any())
                {
                    // Convert JSON array logic or CSV, letting us use string join for simple DB mapping:
                    incident.ExtractedSymptoms = string.Join(", ", extractionResult.Symptoms);
                }
            }
            incident.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Repository<Incident, Guid>().UpdateAsync(incident);
            var saved = await _unitOfWork.SaveChangesAsync();

            if (saved <= 0)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Fail0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001)
                );
            }

            _logger.LogInformation("Voice symptom added to Incident {IncidentId} by Victim {UserId}", incidentId, userId);

            return new ServiceResult(
                ResultCodeConst.Incident_Success0007,
                await _msgService.GetMessageAsync(ResultCodeConst.Incident_Success0007),
                new { url = uploadResult.Url }
            );
        }

        #endregion

        #region SOS Pre-Check

        /// <inheritdoc />
        public async Task<IServiceResult> GetSosEligibilityAsync(Guid userId)
        {
            if (userId == Guid.Empty)
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013)
                );

            // Pure Redis read — no DB access, no incident created.
            // Fail-open: if Redis is down, allow SOS (safety-critical feature).
            var spamStatus = await _spamGuard.CheckAsync(userId);

            var dto = new SosEligibilityDto
            {
                IsEligible                  = spamStatus != SosSpamStatus.HardBlocked,
                RequiresWarningConfirmation  = spamStatus == SosSpamStatus.SoftWarning,
                BlockReason                 = spamStatus == SosSpamStatus.HardBlocked
                    ? await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0004)
                    : null
            };

            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                dto
            );
        }

        #endregion

        #region Symptom Tracking

        /// <summary>
        /// Update victim's symptoms via Bottom Sheet UI.
        /// Handles 4 cases based on IncidentStatus:
        ///   Case 1 (Dispatching): DB-only — next Tier job auto-picks up fresh data.
        ///   Case 2 (Assigned):    SignalR + FCM to the assigned Rescuer.
        ///   Case 3 (Unassigned):  Broadcast SignalR + FCM to nearby rescuers (like a mini-dispatch).
        ///   Case 5 (Closed/Cancelled): Reject with warning.
        /// </summary>
        public async Task<IServiceResult> UpdateSymptomsAsync(
            Guid userId, Guid incidentId, int? minutesSinceBite, List<SymptomType> symptoms)
        {
            if (userId == Guid.Empty)
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013));

            var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);
            if (incident == null)
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0002,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));

            if (incident.VictimId != userId)
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0007,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0007));

            // Case 5: Reject updates on terminal statuses
            if (incident.CurrentStatus == IncidentStatus.Closed ||
                incident.CurrentStatus == IncidentStatus.Cancelled)
                return new ServiceResult(
                    ResultCodeConst.Incident_Warning0010,
                    await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0010));

            var now = DateTime.UtcNow;
            var symptomsCsv = string.Join(", ", symptoms);

            // Partial update: only overwrite MinutesSinceBite if provided (first submission)
            if (minutesSinceBite.HasValue)
                incident.MinutesSinceBite = minutesSinceBite.Value;

            incident.ExtractedSymptoms = symptomsCsv;
            incident.LastSymptomUpdateAt = now;
            incident.UpdatedAt = now;

            // Timeline snapshot — append-only history for Rescuer's patient chart
            var snapshot = new IncidentSymptom
            {
                Id = Guid.NewGuid(),
                IncidentId = incidentId,
                ReportedBy = userId,
                HasBleeding = symptoms.Contains(SymptomType.Bleeding),
                HasSwelling = symptoms.Contains(SymptomType.Swelling),
                HasNecrosis = symptoms.Contains(SymptomType.Swelling), // Swelling/Necrosis shared flag functionally
                HasBreathingDifficulty = symptoms.Contains(SymptomType.BreathingDifficulty),
                HasPtosis = symptoms.Contains(SymptomType.Ptosis),
                HasVomiting = symptoms.Contains(SymptomType.VomitingDizziness),
                HasPain = symptoms.Contains(SymptomType.Pain),
                Notes = symptomsCsv,
                ReportedAt = now,
                CreatedAt = now,
                CreatedBy = userId
            };
            await _unitOfWork.Repository<IncidentSymptom, Guid>().AddAsync(snapshot);
            await _unitOfWork.SaveChangesAsync();

            // Notification routing based on incident status
            await NotifySymptomUpdateAsync(incident, symptomsCsv);

            _logger.LogInformation(
                "Symptom update saved for IncidentId={Id}. Status={S}. Symptoms={Sym}",
                incidentId, incident.CurrentStatus, symptomsCsv);

            // Determine appropriate AI Voice message to return to Frontend based on clinical priority
            string voiceCode = ResultCodeConst.Voice_None;
            if (symptoms.Contains(SymptomType.BreathingDifficulty))
                voiceCode = ResultCodeConst.Voice_BreathingDifficulty;
            else if (symptoms.Contains(SymptomType.Ptosis))
                voiceCode = ResultCodeConst.Voice_Ptosis;
            else if (symptoms.Contains(SymptomType.Bleeding))
                voiceCode = ResultCodeConst.Voice_Bleeding;
            else if (symptoms.Contains(SymptomType.VomitingDizziness))
                voiceCode = ResultCodeConst.Voice_VomitingDizziness;
            else if (symptoms.Contains(SymptomType.Swelling) || symptoms.Contains(SymptomType.Pain))
                voiceCode = ResultCodeConst.Voice_SwellingPain;

            var voiceMessage = await _msgService.GetMessageAsync(voiceCode);

            return new ServiceResult(
                ResultCodeConst.Incident_Success0008,
                await _msgService.GetMessageAsync(ResultCodeConst.Incident_Success0008),
                new 
                {
                    AiVoiceMessage = voiceMessage 
                });
        }

        /// <summary>
        /// Retrieves the time-series history of symptom updates for a specific incident.
        /// Only accessible by the victim or assigned rescuers.
        /// </summary>
        public async Task<IServiceResult> GetSymptomTimelineAsync(Guid userId, Guid incidentId)
        {
            if (userId == Guid.Empty)
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013));

            // Validate incident access (Victim or Assigned Rescuer)
            var spec = new BaseSpecification<Incident>(i => 
                i.Id == incidentId && 
                (i.VictimId == userId || i.Missions.Any(m => m.RescuerId == userId))
            );
            
            var incident = await _unitOfWork.Repository<Incident, Guid>().GetWithSpecAsync(spec);
            if (incident == null)
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));

            // Fetch symptoms in chronological order
            var symptomSpec = new BaseSpecification<IncidentSymptom>(s => s.IncidentId == incidentId);
            symptomSpec.AddOrderBy(s => s.ReportedAt);
            
            var symptomLogs = await _unitOfWork.Repository<IncidentSymptom, Guid>().GetAllWithSpecAsync(symptomSpec);

            // Map to Timeline DTO
            var timeline = symptomLogs.Select(s => 
            {
                var activeSymptoms = new List<SymptomType>();
                if (s.HasBleeding) activeSymptoms.Add(SymptomType.Bleeding);
                if (s.HasSwelling) activeSymptoms.Add(SymptomType.Swelling);
                if (s.HasBreathingDifficulty) activeSymptoms.Add(SymptomType.BreathingDifficulty);
                if (s.HasPtosis) activeSymptoms.Add(SymptomType.Ptosis);
                if (s.HasVomiting) activeSymptoms.Add(SymptomType.VomitingDizziness);
                if (s.HasPain) activeSymptoms.Add(SymptomType.Pain);
                if (activeSymptoms.Count == 0) activeSymptoms.Add(SymptomType.None);

                return new IncidentSymptomTimelineDto
                {
                    Id = s.Id,
                    ReportedAt = s.ReportedAt,
                    ActiveSymptoms = activeSymptoms,
                    Notes = s.Notes,
                    MinutesSinceBite = incident.MinutesSinceBite
                };
            }).ToList();

            return new ServiceResult(
                ResultCodeConst.SYS_Success0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001),
                timeline);
        }

        /// <summary>
        /// Routes symptom-update notifications based on current incident status.
        /// </summary>
        private async Task NotifySymptomUpdateAsync(Incident incident, string symptomsCsv)
        {
            var incidentId = incident.Id;

            // Build lightweight payload for SignalR
            var signalRPayload = new
            {
                IncidentId = incidentId,
                IncidentCode = incident.Code,
                MinutesSinceBite = incident.MinutesSinceBite,
                Symptoms = symptomsCsv,
                UpdatedAt = incident.LastSymptomUpdateAt
            };

            // Always broadcast to the incident tracking group (victim + observers)
            await _locationHub.Clients
                .Group(LocationConstants.SignalRGroupPrefix + incidentId)
                .SendAsync(DispatchConstants.EventSymptomUpdated, signalRPayload);

            // Case 1 (Dispatching): DB already updated — next Hangfire Tier job will pick up fresh data.
            // No extra notification needed.

            // Case 2 (Assigned / Arrived): Point-to-point to the active Rescuer
            if (incident.CurrentStatus == IncidentStatus.Assigned ||
                incident.CurrentStatus == IncidentStatus.Arrived)
            {
                var missionSpec = new BaseSpecification<RescueMission>(m =>
                    m.IncidentId == incident.Id &&
                    (m.Status == RescueStatus.Accepted || m.Status == RescueStatus.Arrived));
                var activeMission = await _unitOfWork.Repository<RescueMission, Guid>()
                    .GetWithSpecAsync(missionSpec);

                if (activeMission != null)
                {
                    // SignalR direct to rescuer
                    await _rescueHub.Clients
                        .Group(DispatchConstants.RescuerGroupPrefix + activeMission.RescuerId)
                        .SendAsync(DispatchConstants.EventSymptomUpdated, signalRPayload);

                    // FCM high-priority push
                    var body = string.Format(DispatchConstants.PushSymptomBody, incident.Code);
                    var data = new Dictionary<string, string>
                    {
                        { "incidentId", incidentId.ToString() },
                        { "type", DispatchConstants.FcmSymptomUpdateTitleKey }
                    };
                    await _fcmService.SendToUserAsync(
                        activeMission.RescuerId, DispatchConstants.PushSymptomTitle, body, data);

                    await _unitOfWork.Repository<NotificationLog, Guid>().AddAsync(new NotificationLog
                    {
                        Id = Guid.NewGuid(),
                        UserId = activeMission.RescuerId,
                        Title = DispatchConstants.PushSymptomTitle,
                        Message = body,
                        Type = NotificationType.Mission,
                        IsRead = false,
                        SentAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _unitOfWork.SaveChangesAsync();
                }
            }

            // Case 3 (Unassigned): Broadcast to all nearby rescuers like a mini-dispatch
            if (incident.CurrentStatus == IncidentStatus.Unassigned)
            {
                var heartbeatCutoff = DateTime.UtcNow.AddHours(-DispatchConstants.Tier3FreshnessHours);
                var rescuerSpec = new BaseSpecification<User>(u =>
                    u.Status == UserStatus.Active &&
                    u.RescuerProfile != null &&
                    u.RescuerProfile.IsAvailable &&
                    u.RescuerProfile.IsVerified &&
                    u.CurrentLocation != null &&
                    u.LocationUpdatedAt != null &&
                    u.LocationUpdatedAt >= heartbeatCutoff &&
                    u.CurrentLocation.Distance(incident.Location) <= DispatchConstants.Tier3RadiusMeters);

                var rescuers = await _unitOfWork.Repository<User, Guid>().GetAllWithSpecAsync(rescuerSpec);
                var body = string.Format(DispatchConstants.PushSymptomBody, incident.Code);
                var notificationsToSave = new List<NotificationLog>();

                foreach (var rescuer in rescuers)
                {
                    // SignalR per-rescuer
                    await _rescueHub.Clients
                        .Group(DispatchConstants.RescuerGroupPrefix + rescuer.Id)
                        .SendAsync(DispatchConstants.EventSymptomUpdated, signalRPayload);

                    // FCM per-rescuer
                    var data = new Dictionary<string, string>
                    {
                        { "incidentId", incidentId.ToString() },
                        { "type", DispatchConstants.FcmSymptomUpdateTitleKey }
                    };
                    await _fcmService.SendToUserAsync(
                        rescuer.Id, DispatchConstants.PushSymptomTitle, body, data);

                    notificationsToSave.Add(new NotificationLog
                    {
                        Id = Guid.NewGuid(),
                        UserId = rescuer.Id,
                        Title = DispatchConstants.PushSymptomTitle,
                        Message = body,
                        Type = NotificationType.Mission,
                        IsRead = false,
                        SentAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (notificationsToSave.Count > 0)
                {
                    await _unitOfWork.Repository<NotificationLog, Guid>().AddRangeAsync(notificationsToSave);
                    await _unitOfWork.SaveChangesAsync();
                }

                _logger.LogInformation(
                    "Symptom broadcast (Unassigned) sent to {Count} rescuers. IncidentId={Id}",
                    rescuers.Count(), incidentId);
            }
        }

        #endregion
        #region SMS Gateway 

        /// <summary>
        /// Processes an incoming SOS from an SMS via a DIY Gateway.
        /// Extracts GPS, finds the user by phone number, and creates the Incident.
        /// </summary>
        public async Task<IServiceResult> ProcessSmsWebhookAsync(string senderPhone, string messageBody, string secretKey)
        {
            var expectedKey = _configuration["SmsGateway:SecretKey"];
            if (string.IsNullOrEmpty(expectedKey) || secretKey != expectedKey)
            {
                return new ServiceResult(
                    ResultCodeConst.Incident_Warning0011,
                    await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0011)
                );
            }

            // Fallback expected format: "SFARS SOS 10.772,106.698"
            var text = messageBody?.Trim();
            if (string.IsNullOrEmpty(text) || !text.StartsWith(DispatchConstants.SmsSosPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return new ServiceResult(
                    ResultCodeConst.Incident_Warning0012, 
                    await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0012)
                );
            }

            var parts = text.Substring(DispatchConstants.SmsSosPrefix.Length).Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || 
                !double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat) || 
                !double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng))
            {
                return new ServiceResult(
                    ResultCodeConst.Incident_Warning0013, 
                    await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0013)
                );
            }

            // Normalize phone: e.g. +84901234567 -> 0901234567
            var normalizedPhone = NormalizeVnPhoneNumber(senderPhone);

            // Find user in database
            var user = await _unitOfWork.Repository<User, Guid>()
                .GetQueryable(tracked: false)
                .FirstOrDefaultAsync(u => u.Phone == normalizedPhone);

            if (user == null)
            {
                _logger.LogWarning("SMS SOS received but no matching user found for phone: {Phone}", normalizedPhone);
                return new ServiceResult(
                    ResultCodeConst.Incident_Warning0014, 
                    await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0014)
                );
            }

            // Automatically create Incident on behalf of this user
            var dto = new IncidentDto
            {
                Latitude = lat,
                Longitude = lng,
                AddressString = DispatchConstants.SmsAddressFallback,
                Description = DispatchConstants.SmsDescriptionFallback,
                PriorityLevel = SeverityLevel.Critical // Always treat offline SOS as Critical
            };

            var createResult = await CreateIncidentAsync(user.Id, dto);
            if (!createResult.ResultCode.Contains("Success"))
            {
                _logger.LogError("Failed to create Incident from SMS: {Message}", createResult.Message);
                return createResult;
            }

            // [CRITICAL] Trigger the actual SOS dispatch flow (Ping Rescuers)
            // Since it's an offline fallback, we bypass the grace period and dispatch immediately.
            if (createResult.Data is IncidentDto createdIncident)
            {
                BackgroundJob.Enqueue<IDispatchService>(s => s.StartDispatchAsync(createdIncident.Id));
                _logger.LogInformation("SMS SOS Dispatch triggered for Incident {Code}", createdIncident.Code);
            }
            
            return createResult;
        }

        public static string NormalizeVnPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            var clean = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
            if (clean.StartsWith("+84")) clean = "0" + clean.Substring(3);
            if (clean.StartsWith("84")) clean = "0" + clean.Substring(2);
            return clean;
        }

        #endregion
    }
}