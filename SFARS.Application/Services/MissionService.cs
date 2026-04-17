using Hangfire;
using System.Text.Json;
using Mapster;
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
    private readonly IDistributedLockProvider _distributedLockProvider;
    private readonly IDispatchService _dispatchService;
    private readonly IMapper _mapper;

    public MissionService(
        IUnitOfWork unitOfWork,
        IBackgroundJobClient jobs,
        IHubContext<LocationTrackingHub> locationHub,
        ISystemMessageService msgService,
        ILogger<MissionService> logger,
        IFcmPushService fcmService,
        IDistributedLockProvider distributedLockProvider,
        IDispatchService dispatchService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _jobs = jobs;
        _locationHub = locationHub;
        _msgService = msgService;
        _logger = logger;
        _fcmService = fcmService;
        _distributedLockProvider = distributedLockProvider;
        _dispatchService = dispatchService;
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

        var dtos = await _unitOfWork.Repository<RescueMission, Guid>()
            .GetWithSpec(spec, tracked: false)
            .ProjectToType<MissionDto>(_mapper.Config)
            .ToListAsync();

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
        // Load rescuer without tracking for distance and status checks
        var rescuer = await _unitOfWork.Repository<User, Guid>().GetWithSpecAsync(
            new BaseSpecification<User>(u => u.Id == rescuerId), 
            tracked: false);

        if (rescuer?.CurrentLocation == null)
            return new ServiceResult(ResultCodeConst.SYS_Fail0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));

        var activeMissionsSpec = new BaseSpecification<RescueMission>(m => 
           m.RescuerId == rescuerId && 
           (m.Status == RescueStatus.Accepted || m.Status == RescueStatus.Arrived));
        var activeMissions = await _unitOfWork.Repository<RescueMission, Guid>().GetAllWithSpecAsync(activeMissionsSpec);
        
        if (activeMissions.Any())
        {
           _logger.LogWarning("AcceptMissionAsync: Rescuer {RescuerId} tried to accept but already has an active mission.", rescuerId);
           return new ServiceResult(ResultCodeConst.Incident_Warning0007, await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0007)); // Or a new strict warning code like "Already on mission"
        }

        // Load incident without tracking initially (Validation Phase)
        // This prevents the context from holding a stale RowVersion before the atomic SQL update
        var incident = await _unitOfWork.Repository<Incident, Guid>().GetWithSpecAsync(
            new BaseSpecification<Incident>(i => i.Id == incidentId), 
            tracked: false);

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
                IncidentStatus.Assigned.ToString(), DateTime.UtcNow, incidentId,
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
            Status = RescueStatus.Accepted,
            StartedAt = DateTime.UtcNow,
            InitialDistanceMeters = dist, // Captured at line 110
            LastCheckedDistanceMeters = dist,
            NextCheckAt = DateTime.UtcNow.AddMinutes(DispatchConstants.WatchdogInitialGraceMins),
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
            StatusTo = IncidentStatus.Assigned,
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

        // Accepting the mission must cancel the fallback chain immediately.
        CancelDispatchJobs(incident, incidentId);

        // Step 5: Schedule claim timeout job
        _jobs.Schedule<IMissionService>(
            s => s.ClaimTimeoutAsync(mission.Id), 
            TimeSpan.FromMinutes(DispatchConstants.WatchdogInitialGraceMins));

        await _unitOfWork.SaveChangesAsync();

        // Real-time: Notify community that this incident is now Assigned (remove from available pool)
        await _dispatchService.NotifyCommunityAsync(incidentId);

        // Step 6: Notify victim
        var dto = new SosFallbackDto
        {
            IncidentId = incidentId,
            Message = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Success0002) // "Rescuer has accepted the mission."
        };

        await _locationHub.Clients
            .Group(LocationConstants.SignalRGroupPrefix + incidentId)
            .SendAsync(DispatchConstants.EventAssigned, dto);

        // Case 4: Race Condition — detect if victim updated symptoms while rescuer was deciding
        if (incident.LastSymptomUpdateAt.HasValue && incident.LastSymptomUpdateAt > incident.CreatedAt)
        {
            var symptomPayload = new
            {
                IncidentId = incidentId,
                IncidentCode = incident.Code,
                MinutesSinceBite = incident.MinutesSinceBite,
                UpdatedAt = incident.LastSymptomUpdateAt
            };

            await _locationHub.Clients
                .Group(DispatchConstants.RescuerGroupPrefix + rescuerId)
                .SendAsync(DispatchConstants.EventSymptomUpdated, symptomPayload);

            var symptomBody = string.Format(DispatchConstants.PushSymptomBody, incident.Code);
            var symptomData = new Dictionary<string, string>
            {
                { "incidentId", incidentId.ToString() },
                { "type", DispatchConstants.FcmSymptomUpdateTitleKey }
            };
            await _fcmService.SendToUserAsync(rescuerId, DispatchConstants.PushSymptomTitle, symptomBody, symptomData);

            _logger.LogInformation(
                "Case4 Race Condition: Symptom update detected during accept. Rescuer={RescuerId}, Incident={IncidentId}, SymptomUpdate={T}",
                rescuerId, incidentId, incident.LastSymptomUpdateAt);
        }

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
        // 1. DISTRIBUTED LOCK: Anti-race condition for multi-node environments
        await using var @lock = await _distributedLockProvider.TryAcquireLockAsync(
            $"mission_watchdog_{missionId}",
            TimeSpan.FromSeconds(30));

        if (@lock == null)
        {
            _logger.LogWarning("Watchdog: Could not acquire lock for Mission {MissionId}. Skipping execution.", missionId);
            return;
        }

        // 2. LOAD DATA: Include Rescuer and Incident to evaluate real-world progress
        var spec = new BaseSpecification<RescueMission>(m => m.Id == missionId);
        spec.ApplyInclude(q => q.Include(m => m.Rescuer));
        spec.ApplyInclude(q => q.Include(m => m.Incident));

        var mission = await _unitOfWork.Repository<RescueMission, Guid>().GetWithSpecAsync(spec);

        if (mission == null || mission.Status != RescueStatus.Accepted) return;
        var incident = mission.Incident;
        if (incident.CurrentStatus != IncidentStatus.Assigned) return;

        var utcNow = DateTime.UtcNow;

        // 3. STAGNATION DETECTION (GHOSTING & COFFEE-SHOP CHECKS)
        var timeSinceLastUpdate = utcNow - (mission.Rescuer.LocationUpdatedAt ?? mission.CreatedAt);
        bool isGhosted = timeSinceLastUpdate.TotalMinutes > DispatchConstants.WatchdogHeartbeatTimeoutMins;

        bool hasNoProgress = false;
        if (!isGhosted && mission.Rescuer.CurrentLocation != null && incident.Location != null)
        {
            var currentDistToVictim = mission.Rescuer.CurrentLocation.Distance(incident.Location);
            
            // Micro-Check: Did they move >= 15m since their OWN last location ping?
            var microMovementDelta = mission.Rescuer.LastLocationDeltaMeters ?? 0;
            bool microStalled = microMovementDelta < DispatchConstants.WatchdogMicroMovementThresholdMeters;

            // Macro-Check: Did they get >= 500m closer to the victim since OUR last watchdog check (10m ago)?
            var lastCheckedDist = mission.LastCheckedDistanceMeters ?? currentDistToVictim;
            var macroProgress = lastCheckedDist - currentDistToVictim;
            bool macroStalled = macroProgress < DispatchConstants.WatchdogMacroProgressThresholdMeters;

            // Stall criteria: If they move < 15m AND didn't make significant progress towards victim
            if (microStalled && macroStalled)
            {
                hasNoProgress = true;
            }
            
            // Update the macro-baseline for the next interval
            mission.LastCheckedDistanceMeters = currentDistToVictim;
        }

        if (isGhosted || hasNoProgress)
        {
            _logger.LogWarning("Watchdog: Rescuer {RescuerId} stalled (Ghosted: {Ghosted}, Stalled: {Stalled}) for Mission {MissionId}. Reverting...", 
                mission.RescuerId, isGhosted, hasNoProgress, mission.Id);

            // 4. ATOMIC REVERSION & RE-DISPATCH
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Increment version to invalidate any stale accept attempts
                incident.DispatchVersion++;
                incident.CurrentStatus = IncidentStatus.Unassigned;
                incident.UpdatedAt = utcNow;

                mission.Status = RescueStatus.Rejected;
                mission.UpdatedAt = utcNow;

                // Reset AI review snapshot if still pending
                if (incident.CurrentAiReviewStatus == AiReviewStatus.Pending)
                {
                    incident.CurrentAiReviewId = null;
                    incident.CurrentAiReviewStatus = null;
                }

                // Append Audit Trail
                await _unitOfWork.Repository<IncidentStatusHistory, Guid>().AddAsync(new IncidentStatusHistory
                {
                    Id = Guid.NewGuid(),
                    IncidentId = incident.Id,
                    StatusFrom = IncidentStatus.Assigned,
                    StatusTo = IncidentStatus.Unassigned,
                    ChangedBy = Guid.Empty,
                    ChangeReason = await _msgService.GetMessageAsync(isGhosted ? ResultCodeConst.Incident_Reason0006 : ResultCodeConst.Incident_Reason0011),
                    CreatedAt = utcNow
                });

                // 5. TRANSACTIONAL OUTBOX: Schedule notifications as side-effects
                var outboxMsg = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = "MissionReverted",
                    Payload = JsonSerializer.Serialize(new { IncidentId = incident.Id, MissionId = missionId, RescuerId = mission.RescuerId }),
                    CreatedAt = utcNow
                };
                await _unitOfWork.Repository<OutboxMessage, Guid>().AddAsync(outboxMsg);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                // Re-trigger dispatch search immediately
                _jobs.Enqueue<IDispatchService>(s => s.StartDispatchAsync(incident.Id));
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, "Watchdog: Transaction failed for Mission {MissionId}", missionId);
            }
        }
        else
        {
            // 6. RECURSIVE CHAIN: Everything looks good, schedule the next check
            mission.NextCheckAt = utcNow.AddMinutes(DispatchConstants.WatchdogStandardIntervalMins);
            await _unitOfWork.SaveChangesAsync();
System.Diagnostics.Debug.WriteLine($"Watchdog scheduled at {mission.Id}");
            _jobs.Schedule<IMissionService>(
                s => s.ClaimTimeoutAsync(missionId), 
                TimeSpan.FromMinutes(DispatchConstants.WatchdogStandardIntervalMins));
            
            _logger.LogInformation("Watchdog: Mission {MissionId} validated. Next check scheduled at {Next}", missionId, mission.NextCheckAt);
        }
    }

    public async Task<IServiceResult> UpdateStatusAsync(Guid missionId, Guid rescuerId, IncidentStatus newStatus)
    {
        var spec = new BaseSpecification<RescueMission>(m => m.Id == missionId && m.RescuerId == rescuerId);
        spec.ApplyInclude(q => q.Include(m => m.Incident));
        var mission = await _unitOfWork.Repository<RescueMission, Guid>().GetWithSpecAsync(spec);

        if (mission == null) 
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));

        if (mission.Status == RescueStatus.Completed || mission.Status == RescueStatus.Rejected || mission.Status == RescueStatus.Reassigned)
        {
            _logger.LogWarning("UpdateStatusAsync: Rescuer {RescuerId} attempted to update historical mission {MissionId} (Status: {Status})", rescuerId, missionId, mission.Status);
            return new ServiceResult(ResultCodeConst.Mission_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.Mission_Warning0001));
        }

        var incident = mission.Incident;

        if (incident.CurrentStatus == IncidentStatus.Closed || incident.CurrentStatus == IncidentStatus.Cancelled)
            return new ServiceResult(ResultCodeConst.Mission_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.Mission_Warning0001));

        // Sync IncidentStatus to RescueStatus
        if (newStatus == IncidentStatus.Closed)
        {
            mission.Status = RescueStatus.Completed;
            mission.CompletedAt = DateTime.UtcNow;

            // Cleanup: If review is still pending, clear pointers as it's no longer mandatory
            if (incident.CurrentAiReviewStatus == AiReviewStatus.Pending)
            {
                incident.CurrentAiReviewId = null;
                incident.CurrentAiReviewStatus = null;
            }
        }
        else if (newStatus == IncidentStatus.Arrived)
        {
            mission.Status = RescueStatus.Arrived; 
            mission.ArrivedAt = DateTime.UtcNow;
        }
        else if (newStatus == IncidentStatus.Assigned)
        {
            mission.Status = RescueStatus.Accepted;
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
                NewStatus = newStatus.ToString()
            };

            await _locationHub.Clients
                .Group(LocationConstants.SignalRGroupPrefix + incident.Id)
                .SendAsync(LocationConstants.SignalRMissionStatusUpdated, payload);

            // FCM push to victim (app may be backgrounded during rescue)
            var fcmTitle = newStatus == IncidentStatus.Arrived
                ? DispatchConstants.PushMissionArrivedTitle
                : DispatchConstants.PushMissionClosedTitle;
            var fcmBody = newStatus == IncidentStatus.Arrived
                ? DispatchConstants.PushMissionArrivedBody
                : DispatchConstants.PushMissionClosedBody;
            var fcmData = new Dictionary<string, string>
            {
                { "incidentId", incident.Id.ToString() },
                { "type", DispatchConstants.FcmMissionStatusTitleKey },
                { "newStatus", newStatus.ToString() }
            };
            await _fcmService.SendToUserAsync(incident.VictimId, fcmTitle, fcmBody, fcmData);
        }

        return new ServiceResult(ResultCodeConst.Mission_Success0001, await _msgService.GetMessageAsync(ResultCodeConst.Mission_Success0001));
    }

    private void CancelDispatchJobs(Incident incident, Guid incidentId)
    {
        if (string.IsNullOrWhiteSpace(incident.DispatchJobIds))
        {
            return;
        }

        try
        {
            var jobIds = JsonSerializer.Deserialize<string[]>(incident.DispatchJobIds);
            if (jobIds != null)
            {
                foreach (var jobId in jobIds.Where(id => !string.IsNullOrWhiteSpace(id)))
                {
                    _jobs.Delete(jobId);
                }
            }

            incident.DispatchJobIds = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete dispatch jobs for incident {IncidentId}", incidentId);
        }
    }
}