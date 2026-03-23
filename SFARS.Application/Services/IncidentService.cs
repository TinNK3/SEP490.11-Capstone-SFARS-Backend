using Hangfire;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using SFARS.Application.Common;
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
using SFARS.Infrastructure.Configurations;
using SFARS.Infrastructure.Helpers;
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

        public IncidentService(
            ISystemMessageService msgService,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<IncidentService> logger,
            IFileStorageService fileStorageService,
            IOptions<StorageOptions> storageOptions,
            ISosSpamGuardService spamGuard,
            IAiReviewService<SubmitAiReviewRequestDto, FirstAidStepDto> aiReviewService,
            ISpeechToTextService speechToTextService)
            : base(msgService, unitOfWork, mapper, logger)
        {
            _fileStorageService = fileStorageService;
            _storageOptions = storageOptions;
            _spamGuard = spamGuard;
            _aiReviewService = aiReviewService;
            _speechToTextService = speechToTextService;
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

            // Create IncidentChat (empty shell for future AI chat)
            var chat = new IncidentChat
            {
                Id = Guid.NewGuid(),
                IncidentId = incident.Id,
                Title = string.Empty,
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
            chat.Title = $"Chat - {incidentCode}";
            notification.Message = string.Format(notifyTemplate, incidentCode);

            // Add all entities to repositories
            await _unitOfWork.Repository<Incident, Guid>().AddAsync(incident);
            await _unitOfWork.Repository<IncidentStatusHistory, Guid>().AddAsync(statusHistory);
            await _unitOfWork.Repository<IncidentChat, Guid>().AddAsync(chat);
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
        /// Get incidents reported by the current user
        /// </summary>
        public async Task<IServiceResult> GetMyIncidentsAsync(Guid userId, int page = 0, int pageSize = 10)
        {
            if (userId == Guid.Empty)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013)
                );
            }

            var spec = new BaseSpecification<Incident>(i => i.VictimId == userId);
            spec.AddOrderByDescending(i => i.CreatedAt);
            spec.ApplyPaging(pageSize, page * pageSize);

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
                    AiPredictionResult = i.AiPredictionResult,
                    AiConfidenceScore = i.AiConfidenceScore,
                    SnakeId = i.SnakeId,
                    VictimId = i.VictimId,
                    VictimName = i.Victim.FullName,
                    CreatedAt = i.CreatedAt,
                    SymptomAudioUrl = i.SymptomAudioUrl,
                    SymptomText = i.SymptomText,
                    MinutesSinceBite = i.MinutesSinceBite,
                    ExtractedSymptoms = i.ExtractedSymptoms
                }, tracked: false);

            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                dtos
            );
        }

        /// <summary>
        /// Get incident by ID with authorization check (user must be victim or rescuer)
        /// </summary>
        public async Task<IServiceResult> GetIncidentByIdAsync(Guid userId, Guid incidentId)
        {
            if (userId == Guid.Empty)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013)
                );
            }

            var spec = new BaseSpecification<Incident>(i =>
                i.Id == incidentId &&
                (
                    i.VictimId == userId ||
                    i.Missions.Any(m => m.RescuerId == userId)
                )
            );

            var incident = await _unitOfWork.Repository<Incident, Guid>()
                .GetWithSpecAsync(spec, tracked: false);

            if (incident == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004)
                );
            }

            var dto = new IncidentDetailDto
            {
                Id = incident.Id,
                Code = incident.Code,
                Latitude = incident.Location.Y,
                Longitude = incident.Location.X,
                AddressString = incident.AddressString,
                Description = incident.Description,
                CurrentStatus = incident.CurrentStatus,
                PriorityLevel = incident.PriorityLevel,
                AiPredictionResult = incident.AiPredictionResult,
                AiConfidenceScore = incident.AiConfidenceScore,
                SnakeId = incident.SnakeId,
                VictimId = incident.VictimId,
                VictimName = incident.Victim?.FullName,
                CreatedAt = incident.CreatedAt,
                SymptomAudioUrl = incident.SymptomAudioUrl,
                SymptomText = incident.SymptomText,
                MinutesSinceBite = incident.MinutesSinceBite,
                ExtractedSymptoms = incident.ExtractedSymptoms
            };

            // Get FirstAid/Prohibitions from Source-of-Truth
            var (steps, prohibitions) = await _aiReviewService.GetEffectiveFirstAidProtocolAsync(incident);
            dto.FirstAidSteps = steps;
            dto.Prohibitions = prohibitions;

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
                .Where(m => m.Status == RescueStatus.Accepted || m.Status == RescueStatus.Pending)
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
                .Where(m => m.Status == RescueStatus.Accepted || m.Status == RescueStatus.Pending)
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
    }
}