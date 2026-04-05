using Hangfire;
using MapsterMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.Dispatch;
using SFARS.Application.Dtos.Mission;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.Params;
using SFARS.Infrastructure.Hubs;

namespace SFARS.Application.Services;

public class MissionService : IMissionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobClient _jobs;
    private readonly IHubContext<LocationTrackingHub> _locationHub;
    private readonly ISystemMessageService _msgService;
    private readonly ILogger<MissionService> _logger;
    private readonly IFcmPushService _fcmService;
    private readonly IMapper _mapper;

    public MissionService(
        IUnitOfWork unitOfWork,
        IBackgroundJobClient jobs,
        IHubContext<LocationTrackingHub> locationHub,
        ISystemMessageService msgService,
        ILogger<MissionService> logger,
        IFcmPushService fcmService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _jobs = jobs;
        _locationHub = locationHub;
        _msgService = msgService;
        _logger = logger;
        _fcmService = fcmService;
        _mapper = mapper;
    }

    public async Task<IServiceResult> GetMyMissionsAsync(Guid rescuerId, MissionSpecParams specParams)
    {
        if (rescuerId == Guid.Empty)
        {
            return new ServiceResult(
                ResultCodeConst.Auth_Warning0013,
                await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013)
            );
        }

        var countSpec = new MissionSpecification(specParams, rescuerId, isCount: true);
        var totalItems = await _unitOfWork.Repository<RescueMission, Guid>().CountAsync(countSpec);

        var spec = new MissionSpecification(specParams, rescuerId, isCount: false);

        var entities = await _unitOfWork.Repository<RescueMission, Guid>().GetAllWithSpecAsync(spec);
        var dtos = _mapper.Map<IEnumerable<MissionDto>>(entities);

        var limit = specParams.GetTake();
        var page = specParams.GetPage();
        var totalPages = limit > 0 ? (int)Math.Ceiling(totalItems / (double)limit) : 0;

        var pagedResult = new PaginatedResultDto<MissionDto>(
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

    public async Task<IServiceResult> AcceptMissionAsync(Guid incidentId, Guid rescuerId)
    {
        var rescuer = await _unitOfWork.Repository<User, Guid>().GetByIdAsync(rescuerId);
        if (rescuer?.CurrentLocation == null)
            return new ServiceResult(ResultCodeConst.SYS_Fail0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));

        // Refactor: Prevent rescuer spam accept / active mission overlap
        var activeMissionsSpec = new BaseSpecification<RescueMission>(m => 
            m.RescuerId == rescuerId && 
            (m.Status == RescueStatus.Pending || m.Status == RescueStatus.Accepted));
        var activeMissions = await _unitOfWork.Repository<RescueMission, Guid>().GetAllWithSpecAsync(activeMissionsSpec);
        
        if (activeMissions.Any())
        {
            _logger.LogWarning("AcceptMissionAsync: Rescuer {RescuerId} tried to accept but already has an active mission.", rescuerId);
            return new ServiceResult(ResultCodeConst.Incident_Warning0007, await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0007)); // Or a new strict warning code like "Already on mission"
        }

        var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);
        if (incident == null)
            return new ServiceResult(ResultCodeConst.SYS_Warning0002, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));

        // Fail-fast: Re-check true distance
        var dist = rescuer.CurrentLocation.Distance(incident.Location);
        if (dist > DispatchConstants.FailFastRadiusMeters)
            return new ServiceResult(ResultCodeConst.Incident_Warning0008, await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0008));

        // Step 1: Atomic update to prevent race conditions
        int rowsAffected = 0;
        try 
        {
            rowsAffected = await _unitOfWork.ExecuteSqlRawAsync(
                @"UPDATE Incident SET current_status = {0}, updated_at = {1}
                  WHERE id = {2} AND current_status IN ({3}, {4}, {5}, {6})",
                IncidentStatus.EnRoute.ToString(), DateTime.UtcNow, incidentId,
                IncidentStatus.Dispatching_Tier1.ToString(),
                IncidentStatus.Dispatching_Tier2.ToString(),
                IncidentStatus.Dispatching_Tier3.ToString(),
                IncidentStatus.Unassigned.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute atomic update for incident {id}", incidentId);
        }

        if (rowsAffected == 0)
        {
            _logger.LogWarning("AcceptMissionAsync: Race condition caught or already claimed. Rescuer={RescuerId}, Incident={IncidentId}", rescuerId, incidentId);
            return new ServiceResult(ResultCodeConst.Incident_Warning0007, await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0007));
        }

        // We won the race! Refresh status in memory from DB to avoid staleness
        incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);
        if (incident == null) return new ServiceResult(ResultCodeConst.SYS_Fail0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));

        // Initialize AI Review if there is an AI Inference
        if (incident.CurrentAiInferenceId.HasValue && !incident.CurrentAiReviewId.HasValue)
        {
            var review = new AiInferenceReview
            {
                Id = Guid.NewGuid(),
                IncidentId = incidentId,
                AiInferenceId = incident.CurrentAiInferenceId.Value,
                ReviewerId = rescuerId,
                ReviewStatus = AiReviewStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = rescuerId
            };
            await _unitOfWork.Repository<AiInferenceReview, Guid>().AddAsync(review);

            incident.CurrentAiReviewId = review.Id;
            incident.CurrentAiReviewStatus = AiReviewStatus.Pending;
        }

        // Step 2: Create single RescueMission
        var mission = new RescueMission
        {
            Id = Guid.NewGuid(),
            IncidentId = incidentId,
            RescuerId = rescuerId,
            Status = RescueStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = rescuerId
        };
        await _unitOfWork.Repository<RescueMission, Guid>().AddAsync(mission);

        // Audit Trail
        var statusHistory = new IncidentStatusHistory
        {
            Id = Guid.NewGuid(),
            IncidentId = incidentId,
            StatusFrom = incident.CurrentStatus, // snapshot before atomic UPDATE
            StatusTo = IncidentStatus.EnRoute,
            ChangedBy = rescuerId,
            ChangeReason = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Reason0003),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = rescuerId
        };
        await _unitOfWork.Repository<IncidentStatusHistory, Guid>().AddAsync(statusHistory);

        // NotificationLog for Victim
        var rescuerUser = await _unitOfWork.Repository<User, Guid>().GetByIdAsync(rescuerId);
        var notifyTitle = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Notify0003);
        var notifyBody = string.Format(
            await _msgService.GetMessageAsync(ResultCodeConst.Incident_Success0002),
            rescuerUser?.FullName ?? "Rescuer");

        await _unitOfWork.Repository<NotificationLog, Guid>().AddAsync(new NotificationLog
        {
            Id = Guid.NewGuid(),
            UserId = incident.VictimId,
            Title = notifyTitle,
            Message = notifyBody,
            Type = NotificationType.Mission,
            IsRead = false,
            SentAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = rescuerId
        });

        await _fcmService.SendToUserAsync(incident.VictimId, notifyTitle, notifyBody);

        // Clear Hangfire Jobs
        if (!string.IsNullOrEmpty(incident.DispatchJobIds))
        {
            try
            {
                var jobIds = System.Text.Json.JsonSerializer.Deserialize<string[]>(incident.DispatchJobIds);
                if (jobIds != null)
                {
                    foreach (var jobId in jobIds)
                        _jobs.Delete(jobId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete dispatch jobs for incident {IncidentId}", incidentId);
            }
        }

        // Step 5: Schedule claim timeout job
        _jobs.Schedule<IMissionService>(
            s => s.ClaimTimeoutAsync(mission.Id), 
            TimeSpan.FromMinutes(DispatchConstants.ClaimTimeoutMinutes));

        await _unitOfWork.SaveChangesAsync();

        // Step 6: Notify victim
        var dto = new SosFallbackDto
        {
            IncidentId = incidentId,
            Message = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Success0002) // "Rescuer has accepted the mission."
        };

        await _locationHub.Clients
            .Group(LocationConstants.SignalRGroupPrefix + incidentId)
            .SendAsync(DispatchConstants.EventAssigned, dto);

        _logger.LogInformation("Mission {MissionId} successfully claimed by Rescuer {RescuerId} for Incident {IncidentId}", mission.Id, rescuerId, incidentId);
        
        return new ServiceResult(
            ResultCodeConst.SYS_Success0003,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0003),
            new 
            {
                MissionId = mission.Id,
                IncidentId = incidentId,
                ReviewStatus = incident.CurrentAiReviewStatus?.ToString() ?? "None",
                RequiresReview = incident.CurrentAiReviewId.HasValue,
                AcceptedAt = mission.CreatedAt
            });
    }

    [Queue(DispatchConstants.HangfireQueue)]
    public async Task ClaimTimeoutAsync(Guid missionId)
    {
        var spec = new BaseSpecification<RescueMission>(m => m.Id == missionId);
        spec.ApplyInclude(q => q.Include(m => m.Rescuer));
        
        var mission = await _unitOfWork.Repository<RescueMission, Guid>()
            .GetWithSpecAsync(spec);

        if (mission == null) return;
        if (mission.Status != RescueStatus.Pending) return;

        var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(mission.IncidentId);
        if (incident == null || (incident.CurrentStatus != IncidentStatus.Assigned && incident.CurrentStatus != IncidentStatus.EnRoute)) return;

        // Has rescuer ghosted without refreshing location?
        // Check if the last location update is older than the configured timeout limit
        var timeSinceLastUpdate = DateTime.UtcNow - (mission.Rescuer.LocationUpdatedAt ?? mission.CreatedAt);
        
        if (timeSinceLastUpdate > TimeSpan.FromMinutes(DispatchConstants.ClaimTimeoutMinutes))
        {
            _logger.LogWarning("ClaimTimeout: Rescuer {RescuerId} ghosted Mission {MissionId}. Last update {Time} mins ago. Reverting...", mission.RescuerId, mission.Id, timeSinceLastUpdate.TotalMinutes);
            
            // Revert state
            mission.Status = RescueStatus.Rejected;
            mission.UpdatedAt = DateTime.UtcNow;

            var oldStatus = incident.CurrentStatus;
            incident.CurrentStatus = IncidentStatus.Unassigned;
            
            // Revert review snapshot so new rescuer can review
            if (incident.CurrentAiReviewStatus == AiReviewStatus.Pending || incident.CurrentAiReviewStatus == AiReviewStatus.Deferred)
            {
                if (incident.CurrentAiReviewId.HasValue)
                {
                    var oldReview = await _unitOfWork.Repository<AiInferenceReview, Guid>().GetByIdAsync(incident.CurrentAiReviewId.Value);
                    if (oldReview != null)
                    {
                        // Abandon the unfinalized review
                        oldReview.ReviewStatus = AiReviewStatus.Abandoned;
                        oldReview.Comment = "Invalidated due to mission timeout";
                        oldReview.UpdatedAt = DateTime.UtcNow;
                        oldReview.UpdatedBy = null; // System
                        
                        await _unitOfWork.Repository<AiInferenceReview, Guid>().UpdateAsync(oldReview);
                    }
                }

                incident.CurrentAiReviewId = null;
                incident.CurrentAiReviewStatus = null;
                
                // Clear any rogue human snapshot that might have been set incorrectly
                incident.HumanReviewedSnakeId = null;
                incident.HumanReviewedToxinGroup = null;
            }
            incident.UpdatedAt = DateTime.UtcNow;

            // Audit Trail for Timeout Revert
            await _unitOfWork.Repository<IncidentStatusHistory, Guid>().AddAsync(new IncidentStatusHistory
            {
                Id = Guid.NewGuid(),
                IncidentId = incident.Id,
                StatusFrom = oldStatus,
                StatusTo = IncidentStatus.Unassigned,
                ChangedBy = Guid.Empty, // System-initiated
                ChangeReason = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Reason0006),
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync();

            // Notify victim that rescuer cancelled/timed out, searching... (could trigger RunTier1 again, but for now just fallback to Unassigned)
            var message = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Notify0005);
            var title = "Hệ thống - Ca cứu hộ";
            var dto = new SosFallbackDto
            {
                IncidentId = incident.Id,
                Message = message
            };
            await _locationHub.Clients
                .Group(LocationConstants.SignalRGroupPrefix + incident.Id)
                .SendAsync(DispatchConstants.EventFallback, dto);
            
            // FCM Notification & NotificationLog for Victim
            await _fcmService.SendToUserAsync(incident.VictimId, title, message);
            await _unitOfWork.Repository<NotificationLog, Guid>().AddAsync(new NotificationLog
            {
                Id = Guid.NewGuid(),
                UserId = incident.VictimId,
                Title = title,
                Message = message,
                Type = NotificationType.Mission,
                IsRead = false,
                SentAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });

            // FCM Notification & NotificationLog for Rescuer
            var rescuerMsg = await _msgService.GetMessageAsync(ResultCodeConst.Mission_Notify0002);
            await _fcmService.SendToUserAsync(mission.RescuerId, title, rescuerMsg);
            await _unitOfWork.Repository<NotificationLog, Guid>().AddAsync(new NotificationLog
            {
                Id = Guid.NewGuid(),
                UserId = mission.RescuerId,
                Title = title,
                Message = rescuerMsg,
                Type = NotificationType.Mission,
                IsRead = false,
                SentAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync(); // save logs
            
            // Re-trigger dispatch?
            // To ensure reliability, we can enqueue StartDispatchAsync.
            _jobs.Enqueue<IDispatchService>(s => s.StartDispatchAsync(incident.Id));
        }
    }

    public async Task<IServiceResult> UpdateStatusAsync(Guid missionId, Guid rescuerId, IncidentStatus newStatus)
    {
        var spec = new BaseSpecification<RescueMission>(m => m.Id == missionId && m.RescuerId == rescuerId);
        spec.ApplyInclude(q => q.Include(m => m.Incident));
        var mission = await _unitOfWork.Repository<RescueMission, Guid>().GetWithSpecAsync(spec);

        if (mission == null) 
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));

        var incident = mission.Incident;

        if (incident.CurrentStatus == IncidentStatus.Closed || incident.CurrentStatus == IncidentStatus.Cancelled)
            return new ServiceResult(ResultCodeConst.Mission_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.Mission_Warning0001));

        // Enforce AI Review before closing the Incident
        if (newStatus == IncidentStatus.Closed)
        {
            if (incident.CurrentAiReviewId.HasValue && 
               (incident.CurrentAiReviewStatus == AiReviewStatus.Pending || incident.CurrentAiReviewStatus == AiReviewStatus.Deferred))
            {
                return new ServiceResult(ResultCodeConst.AiReview_Warning_Pending, await _msgService.GetMessageAsync(ResultCodeConst.AiReview_Warning_Pending));
            }
            
            mission.Status = RescueStatus.Completed;
        }
        else
        {
            // Transition RescueStatus off Pending if they update to Arrived
            if (newStatus == IncidentStatus.Arrived)
            {
                mission.Status = RescueStatus.Accepted; 
            }
        }

        // Capture old status BEFORE mutation
        var oldStatus = incident.CurrentStatus;

        incident.CurrentStatus = newStatus;
        incident.UpdatedAt = DateTime.UtcNow;
        mission.UpdatedAt = DateTime.UtcNow;

        // Audit Trail
        var reasonCode = newStatus switch
        {
            IncidentStatus.Arrived => ResultCodeConst.Incident_Reason0004,
            IncidentStatus.Closed  => ResultCodeConst.Incident_Reason0005,
            _                      => ResultCodeConst.Incident_Reason0003
        };
        await _unitOfWork.Repository<IncidentStatusHistory, Guid>().AddAsync(new IncidentStatusHistory
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            StatusFrom = oldStatus,
            StatusTo = newStatus,
            ChangedBy = rescuerId,
            ChangeReason = await _msgService.GetMessageAsync(reasonCode),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = rescuerId
        });

        // NotificationLog for Victim
        if (newStatus == IncidentStatus.Arrived || newStatus == IncidentStatus.Closed)
        {
            var notifyMsg = newStatus == IncidentStatus.Arrived
                ? await _msgService.GetMessageAsync(ResultCodeConst.Incident_Notify0004)
                : await _msgService.GetMessageAsync(ResultCodeConst.Incident_Notify0001);

            await _unitOfWork.Repository<NotificationLog, Guid>().AddAsync(new NotificationLog
            {
                Id = Guid.NewGuid(),
                UserId = incident.VictimId,
                Title = newStatus == IncidentStatus.Arrived ? "Rescuer đã tới nơi" : "Ca cấp cứu đã đóng",
                Message = notifyMsg,
                Type = NotificationType.Mission,
                IsRead = false,
                SentAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = rescuerId
            });
        }

        await _unitOfWork.SaveChangesAsync();

        // SignalR: Only broadcast status changes that are meaningful to the victim
        if (newStatus == IncidentStatus.Arrived || newStatus == IncidentStatus.Closed)
        {
            var payload = new
            {
                IncidentId = incident.Id,
                MissionId = mission.Id,
                OldStatus = oldStatus.ToString(),
                NewStatus = newStatus.ToString(),
                IsReviewMissing = incident.CurrentAiReviewId.HasValue && (incident.CurrentAiReviewStatus == AiReviewStatus.Pending || incident.CurrentAiReviewStatus == AiReviewStatus.Deferred)
            };

            await _locationHub.Clients
                .Group(LocationConstants.SignalRGroupPrefix + incident.Id)
                .SendAsync(LocationConstants.SignalRMissionStatusUpdated, payload);
        }

        return new ServiceResult(ResultCodeConst.Mission_Success0001, await _msgService.GetMessageAsync(ResultCodeConst.Mission_Success0001));
    }
}