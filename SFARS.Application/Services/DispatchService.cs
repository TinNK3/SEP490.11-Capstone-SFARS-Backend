using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Dispatch;
using SFARS.Application.Dtos.Incident;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications;
using SFARS.Infrastructure.Helpers;
using SFARS.Infrastructure.Hubs;
using StackExchange.Redis;
using System.Text.Json;

namespace SFARS.Application.Services;

/// <summary>
/// Tiered SOS dispatch service.
/// </summary>
public class DispatchService : IDispatchService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<RescueDispatchHub> _rescueHub;
    private readonly IHubContext<LocationTrackingHub> _locationHub;
    private readonly ISystemMessageService _msgService;
    private readonly ILogger<DispatchService> _logger;
    private readonly IBackgroundJobClient _jobs;
    private readonly IFcmPushService _fcmService;
    private readonly IConnectionMultiplexer _redis;

    public DispatchService(
        IUnitOfWork unitOfWork,
        IHubContext<RescueDispatchHub> rescueHub,
        IHubContext<LocationTrackingHub> locationHub,
        ISystemMessageService msgService,
        ILogger<DispatchService> logger,
        IBackgroundJobClient jobs,
        IFcmPushService fcmService,
        IConnectionMultiplexer redis)
    {
        _unitOfWork = unitOfWork;
        _rescueHub = rescueHub;
        _locationHub = locationHub;
        _msgService = msgService;
        _logger = logger;
        _jobs = jobs;
        _fcmService = fcmService;
        _redis = redis;
    }

    /// <inheritdoc />
    public async Task StartDispatchAsync(Guid incidentId)
    {
        var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);
        if (incident is null || !CanStartDispatch(incident.CurrentStatus))
        {
            _logger.LogWarning("StartDispatchAsync skipped. IncidentId={Id} status={S}",
                incidentId, incident?.CurrentStatus);
            return;
        }

        // 1. Fail-fast check first (Superset of all tiers — 20km)
        // If no rescuer exists within FailFast radius, abort immediately to notify victim.
        var anyRescuer = await AnyAvailableRescuerWithinAsync(incident.Location, DispatchConstants.FailFastRadiusMeters);
        if (!anyRescuer)
        {
            _logger.LogWarning("Fail-fast: no available rescuer within {R} km for IncidentId={Id}. Triggering fallback.",
                DispatchConstants.FailFastRadiusMeters / 1000, incidentId);
            await RunFallbackAsync(incidentId);
            return;
        }

        // 2. Commit status change FIRST.
        // This ensures that when RunTier1 starts (even if it's instant), it sees the committed status in the DB.
        incident.CurrentStatus = IncidentStatus.Dispatching_Tier1;
        incident.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        // 3. Enqueue/Schedule all search tiers in Hangfire.
        // Contention check: RunTier1 might start precisely now. 
        // Since we already committed the status above, RunTier1 will correctly see 'Dispatching_Tier1'.
        var j1 = _jobs.Schedule<IDispatchService>(
            q => q.RunTier1Async(incidentId), DispatchConstants.Tier1Delay);

        var j2 = _jobs.Schedule<IDispatchService>(
            q => q.RunTier2Async(incidentId), DispatchConstants.Tier2Delay);

        var j3 = _jobs.Schedule<IDispatchService>(
            q => q.RunTier3Async(incidentId), DispatchConstants.Tier3Delay);

        var jf = _jobs.Schedule<IDispatchService>(
            q => q.RunFallbackAsync(incidentId), DispatchConstants.FallbackDelay);

        // 4. Update Job IDs for tracking/cancellation logic.
        // We use ExecuteUpdateAsync here to avoid RowVersion conflicts with RunTier1
        // which might already be running and trying to update its own status.
        var jobIdsJson = JsonSerializer.Serialize(new[] { j1, j2, j3, jf });
        await _unitOfWork.Repository<Incident, Guid>().GetQueryable()
            .Where(i => i.Id == incidentId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.DispatchJobIds, jobIdsJson));

        // 5. Broadcast Community Update
        await NotifyCommunityAsync(incidentId);

        _logger.LogInformation("Dispatch chain started for IncidentId={Id}. Jobs={Jobs}",
            incidentId, jobIdsJson);
    }

    /// <inheritdoc />
    [Queue(DispatchConstants.HangfireQueue)]
    public async Task RunTier1Async(Guid incidentId)
        => await RunTierInternalAsync(incidentId, tier: 1,
            expectedStatus: IncidentStatus.Dispatching_Tier1, 
            radiusMeters: DispatchConstants.Tier1RadiusMeters, 
            maxEtaMinutes: DispatchConstants.Tier1EtaMinutes,
            nextStatus: IncidentStatus.Dispatching_Tier2,
            freshnessMinutes: DispatchConstants.LocationHeartbeatWindowMinutes,
            maxRescuers: DispatchConstants.Tier1MaxRescuers);

    /// <inheritdoc />
    [Queue(DispatchConstants.HangfireQueue)]
    public async Task RunTier2Async(Guid incidentId)
        => await RunTierInternalAsync(incidentId, tier: 2,
            expectedStatus: IncidentStatus.Dispatching_Tier2, 
            radiusMeters: DispatchConstants.Tier2RadiusMeters, 
            maxEtaMinutes: DispatchConstants.Tier2EtaMinutes,
            nextStatus: IncidentStatus.Dispatching_Tier3,
            freshnessMinutes: DispatchConstants.Tier2FreshnessHours * 60,
            maxRescuers: DispatchConstants.Tier2MaxRescuers);

    /// <inheritdoc />
    [Queue(DispatchConstants.HangfireQueue)]
    public async Task RunTier3Async(Guid incidentId)
        => await RunTierInternalAsync(incidentId, tier: 3,
            expectedStatus: IncidentStatus.Dispatching_Tier3, 
            radiusMeters: DispatchConstants.Tier3RadiusMeters, 
            maxEtaMinutes: DispatchConstants.Tier3EtaMinutes,
            nextStatus: null,
            freshnessMinutes: DispatchConstants.Tier3FreshnessHours * 60,
            maxRescuers: DispatchConstants.Tier3MaxRescuers);

    /// <inheritdoc />
    [Queue(DispatchConstants.HangfireQueue)]
    public async Task RunFallbackAsync(Guid incidentId)
    {
        var incident = await _unitOfWork.Repository<Incident, Guid>().GetQueryable(tracked: false)
            .FirstOrDefaultAsync(i => i.Id == incidentId);

        if (incident is null)
        {
            _logger.LogInformation("Fallback skipped because incident was not found. IncidentId={Id}", incidentId);
            return;
        }

        if (!IsFallbackEligibleStatus(incident.CurrentStatus))
        {
            _logger.LogInformation(
                "Fallback skipped because incident is no longer dispatching. Status={Status}. IncidentId={Id}",
                incident.CurrentStatus, incidentId);
            return;
        }

        var hasActiveMission = await _unitOfWork.Repository<RescueMission, Guid>()
            .AnyAsync(new BaseSpecification<RescueMission>(m =>
                m.IncidentId == incidentId &&
                (m.Status == RescueStatus.Accepted || m.Status == RescueStatus.Arrived)));

        if (hasActiveMission)
        {
            _logger.LogInformation(
                "Fallback skipped because incident already has an active mission. IncidentId={Id}",
                incidentId);
            return;
        }

        // Capture old status BEFORE mutation for accurate audit trail
        var oldStatus = incident.CurrentStatus;

        // Atomic update for status and clearing job IDs
        var affectedRows = await _unitOfWork.Repository<Incident, Guid>().GetQueryable()
            .Where(i => i.Id == incidentId && (
                i.CurrentStatus == IncidentStatus.Pending ||
                i.CurrentStatus == IncidentStatus.Dispatching_Tier1 ||
                i.CurrentStatus == IncidentStatus.Dispatching_Tier2 ||
                i.CurrentStatus == IncidentStatus.Dispatching_Tier3))
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.CurrentStatus, IncidentStatus.Unassigned)
                .SetProperty(i => i.DispatchJobIds, (string?)null)
                .SetProperty(i => i.UpdatedAt, DateTime.UtcNow));

        if (affectedRows == 0)
        {
            _logger.LogInformation("Fallback skipped: status already transitioned. IncidentId={Id}", incidentId);
            return;
        }

        // Audit Trail for Fallback
        await _unitOfWork.Repository<IncidentStatusHistory, Guid>().AddAsync(new IncidentStatusHistory
        {
            Id = Guid.NewGuid(),
            IncidentId = incidentId,
            StatusFrom = oldStatus,
            StatusTo = IncidentStatus.Unassigned,
            ChangedBy = Guid.Empty, // System-initiated
            ChangeReason = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Reason0007),
            CreatedAt = DateTime.UtcNow
        });
        
        // Final SaveChanges for audit and notifications below
        await _unitOfWork.SaveChangesAsync();

        var message = await _msgService.GetMessageAsync(ResultCodeConst.Dispatch_Notify0002);
        var fallbackDto = new SosFallbackDto
        {
            IncidentId = incidentId,
            Message = message
        };

        await _locationHub.Clients
            .User(incident.VictimId.ToString())
            .SendAsync(DispatchConstants.EventFallback, fallbackDto);

        // Real-time: Notify community about status change to Unassigned
        await NotifyCommunityAsync(incidentId);

        var fcmTitle = "Cập nhật yêu cầu cứu hộ SOS";
        var fcmData = new Dictionary<string, string>
        {
            { "incidentId", incidentId.ToString() },
            { "type", "sos_fallback" }
        };

        await _fcmService.SendToUserAsync(incident.VictimId, fcmTitle, message, fcmData);

        await _unitOfWork.Repository<NotificationLog, Guid>().AddAsync(new NotificationLog
        {
            Id = Guid.NewGuid(),
            UserId = incident.VictimId,
            Title = fcmTitle,
            Message = message,
            Type = NotificationType.System,
            IsRead = false,
            SentAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        await _unitOfWork.SaveChangesAsync();

        // Schedule an automatic closure to prevent "Ghost SOS" if no rescuer ever accepts
        _jobs.Schedule<IDispatchService>(
            s => s.AutoCloseAbandonedIncidentAsync(incidentId),
            TimeSpan.FromHours(DispatchConstants.AbandonedIncidentExpiryHours));

        _logger.LogWarning("SOS Fallback triggered for IncidentId={Id}. Status=Unassigned. Auto-close scheduled in {H}h.",
            incidentId, DispatchConstants.AbandonedIncidentExpiryHours);
    }

    /// <inheritdoc />
    [Queue(DispatchConstants.HangfireQueue)]
    public async Task AutoCloseAbandonedIncidentAsync(Guid incidentId)
    {
        var incident = await _unitOfWork.Repository<Incident, Guid>().GetQueryable(tracked: false)
            .FirstOrDefaultAsync(i => i.Id == incidentId);
        if (incident == null || incident.CurrentStatus != IncidentStatus.Unassigned)
        {
            return; // Already handled, assigned, or cancelled
        }

        _logger.LogWarning("Auto-closing abandoned incident {Id} after {H} hours of inactivity.", 
            incidentId, DispatchConstants.AbandonedIncidentExpiryHours);

        var oldStatus = incident.CurrentStatus;

        // Atomic update for final closure
        var affectedRows = await _unitOfWork.Repository<Incident, Guid>().GetQueryable()
            .Where(i => i.Id == incidentId && i.CurrentStatus == IncidentStatus.Unassigned)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.CurrentStatus, IncidentStatus.Closed)
                .SetProperty(i => i.UpdatedAt, DateTime.UtcNow));

        if (affectedRows == 0) return;

        await _unitOfWork.Repository<IncidentStatusHistory, Guid>().AddAsync(new IncidentStatusHistory
        {
            Id = Guid.NewGuid(),
            IncidentId = incidentId,
            StatusFrom = oldStatus,
            StatusTo = IncidentStatus.Closed,
            ChangedBy = Guid.Empty, // System
            ChangeReason = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Reason0010),
            CreatedAt = DateTime.UtcNow
        });

        await _unitOfWork.SaveChangesAsync();

        // Real-time: Notify community that this abandoned incident is now officially closed
        await NotifyCommunityAsync(incidentId);

        _logger.LogWarning("Auto-closed abandoned incident {Id} and notified community.", incidentId);
    }

    private async Task RunTierInternalAsync(
        Guid incidentId,
        int tier,
        IncidentStatus expectedStatus,
        double radiusMeters,
        int maxEtaMinutes,
        IncidentStatus? nextStatus,
        int freshnessMinutes,
        int maxRescuers)
    {
        var spec = new BaseSpecification<Incident>(i => i.Id == incidentId);
        spec.ApplyInclude(q => q
            .Include(i => i.Medias)
            .Include(i => i.CurrentAiInference!)
                .ThenInclude(ai => ai.SelectedSnake!));
        
        var incident = await _unitOfWork.Repository<Incident, Guid>().GetWithSpecAsync(spec, tracked: false);

        if (incident is null || incident.CurrentStatus == IncidentStatus.Assigned || incident.CurrentStatus == IncidentStatus.Cancelled)
        {
            _logger.LogInformation("Tier{T} skipped (status is {S}). IncidentId={Id}", tier, incident?.CurrentStatus, incidentId);
            return;
        }

        if (incident.CurrentStatus != expectedStatus)
        {
            _logger.LogInformation("Tier{T} expected {E} but was {S}. IncidentId={Id}", tier, expectedStatus, incident.CurrentStatus, incidentId);
            return; // Out of sequence
        }

        var candidates = await FindRescuersInRadiusAsync(incidentId, incident.Location, radiusMeters, maxEtaMinutes, freshnessMinutes, maxRescuers);

        if (candidates.Count == 0)
        {
            _logger.LogInformation("Tier{T}: 0 rescuers found. IncidentId={Id}", tier, incidentId);
            if (nextStatus.HasValue)
            {
                // Atomic transition even when no rescuers found
                await _unitOfWork.Repository<Incident, Guid>().GetQueryable()
                    .Where(i => i.Id == incidentId && i.CurrentStatus == expectedStatus)
                    .ExecuteUpdateAsync(setter => setter
                        .SetProperty(i => i.CurrentStatus, nextStatus.Value)
                        .SetProperty(i => i.UpdatedAt, DateTime.UtcNow));
            }
            return;
        }

        if (nextStatus.HasValue)
        {
            // Senior-level atomic state transition:
            // Prevents DbUpdateConcurrencyException by using a direct SQL update for critical state fields.
            // This is immune to RowVersion changes on OTHER fields (like AI results or priority) 
            // that might happen between the Load and Save operations in high-concurrency SOS flows.
            var affectedRows = await _unitOfWork.Repository<Incident, Guid>().GetQueryable()
                .Where(i => i.Id == incidentId && i.CurrentStatus == expectedStatus)
                .ExecuteUpdateAsync(setter => setter
                    .SetProperty(i => i.CurrentStatus, nextStatus.Value)
                    .SetProperty(i => i.UpdatedAt, DateTime.UtcNow));

            if (affectedRows == 0)
            {
                // Status changed by another process (e.g., incident accepted, cancelled, or already escalated)
                _logger.LogWarning("Tier{T}: Atomic status update affected 0 rows. IncidentId={Id} likely moved from {E} already.", 
                    tier, incidentId, expectedStatus);
                return;
            }

            // Sync the in-memory object for subsequent DTO building and logging
            incident.CurrentStatus = nextStatus.Value;
            incident.UpdatedAt = DateTime.UtcNow;
        }

        var now = DateTime.UtcNow;

        var db = _redis.GetDatabase();

        // Calculate AI fields ONCE before the loop for performance
        var media = incident.Medias.FirstOrDefault(m => m.MediaType == MediaType.SnakePhoto);
        var imageUrl = media?.MediaUrl;
        var aiName = incident.AiPredictionResult;
        
        var isAiSkipped = string.Equals(aiName, AiInferenceConstants.UnknownSnake, StringComparison.OrdinalIgnoreCase)
                       || incident.CurrentAiInference?.ModelName == AiInferenceConstants.SkippedModelName;
        
        var toxin = incident.CurrentAiInference?.SelectedSnake?.ToxinGroup.ToString();
        var aiConfidence = incident.AiConfidenceScore;
        var priorityText = incident.PriorityLevel.ToString();

        var baseTitleMsg = DispatchConstants.PushTitlePrefix;
        var defaultUnknownSnake = DispatchConstants.PushUnknownSnake;
        var topSnake = isAiSkipped ? defaultUnknownSnake : (aiName ?? defaultUnknownSnake);
        var notificationsToSave = new List<NotificationLog>();

        foreach (var rescuer in candidates)
        {
            var dedupKey = $"dispatch:{incidentId}:{rescuer.Id}";
            // Atomic SETNX to prevent race conditions and double dispatch
            var locked = await db.StringSetAsync(dedupKey, "1", TimeSpan.FromHours(3), When.NotExists);
            if (!locked) continue;
            
            var distKm = LocationHelper.HaversineMeters(rescuer.CurrentLocation!, incident.Location) / 1000.0;
            var distRounded = Math.Round(distKm, 2);
            var etaMin = (int)Math.Ceiling(distKm / DispatchConstants.AvgSpeedKmh * 60);

            var dto = new SosDispatchNotificationDto
            {
                IncidentId = incidentId,
                IncidentCode = incident.Code,
                Latitude = incident.Location.Y,
                Longitude = incident.Location.X,
                AddressString = incident.AddressString,
                PriorityLevel = priorityText,
                DistanceKm = distRounded,
                EstimatedEtaMin = etaMin,
                Tier = tier,
                DispatchedAt = now,

                // Fields for Rich UI Push (Summary payload)
                IncidentImageUrl = imageUrl,
                IsAiSkipped = isAiSkipped,
                AiPrimarySnakeName = aiName,
                AiConfidence = aiConfidence,
                ToxinGroup = toxin,
                SymptomAudioUrl = incident.SymptomAudioUrl,
                MinutesSinceBite = incident.MinutesSinceBite
            };

            await _rescueHub.Clients
                .Group(DispatchConstants.RescuerGroupPrefix + rescuer.Id)
                .SendAsync(DispatchConstants.EventNewDispatch, dto);

            var bodyMsg = string.Format(DispatchConstants.PushBodyTemplate, topSnake, Math.Round(distKm, 1));

            var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var serializedDto = JsonSerializer.Serialize(dto, jsonOpts);
            var parsedPayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(serializedDto);

            var data = new Dictionary<string, string>();
            if (parsedPayload != null)
            {
                foreach (var kvp in parsedPayload)
                {
                    data[kvp.Key] = kvp.Value.ValueKind == JsonValueKind.Null ? string.Empty : kvp.Value.ToString() ?? string.Empty;
                }
            }
            
            data["type"] = DispatchConstants.FcmSosDispatchTitleKey;

            await _fcmService.SendToUserAsync(rescuer.Id, baseTitleMsg, bodyMsg, data);

            // Store FCM notification in memory for batch save
            notificationsToSave.Add(new NotificationLog
            {
                Id = Guid.NewGuid(),
                UserId = rescuer.Id,
                Title = baseTitleMsg,
                Message = bodyMsg,
                Type = NotificationType.Mission,
                IsRead = false,
                SentAt = now,
                CreatedAt = now
            });
        }

        if (notificationsToSave.Any())
        {
            await _unitOfWork.Repository<NotificationLog, Guid>().AddRangeAsync(notificationsToSave);
            await _unitOfWork.SaveChangesAsync();
        }

        _logger.LogInformation("Tier{T} (SignalR + FCM) dispatched to {Count} rescuers. IncidentId={Id}",
            tier, candidates.Count, incidentId);
    }

    private async Task<List<User>> FindRescuersInRadiusAsync(
        Guid incidentId, Point incidentLocation, double radiusMeters, int maxEtaMinutes, int freshnessMinutes, int maxRescuers)
    {
        var heartbeatCutoff = DateTime.UtcNow.AddMinutes(-freshnessMinutes);

        // STDistance is executed inside SQL Server because it's in the Expression tree of the Specification.
        var spec = new BaseSpecification<User>(u =>
            u.Status == UserStatus.Active &&
            u.RescuerProfile != null &&
            u.RescuerProfile.IsAvailable &&
            u.RescuerProfile.IsVerified &&
            u.CurrentLocation != null &&
            u.LocationUpdatedAt != null &&
            u.LocationUpdatedAt >= heartbeatCutoff &&
            u.CurrentLocation.Distance(incidentLocation) <= radiusMeters);

        var allRescuers = await _unitOfWork.Repository<User, Guid>().GetAllWithSpecAsync(spec);

        var candidates = allRescuers
            .Where(u =>
            {
                var distKm = LocationHelper.HaversineMeters(u.CurrentLocation!, incidentLocation) / 1000.0;
                var etaMin = distKm / DispatchConstants.AvgSpeedKmh * 60;
                return etaMin <= maxEtaMinutes;
            })
            .ToList();

        // Nearest rescuers first
        var sortedCandidates = candidates
            .OrderBy(u => LocationHelper.HaversineMeters(u.CurrentLocation!, incidentLocation))
            .Take(maxRescuers)
            .ToList();

        return sortedCandidates;
    }

    /// <inheritdoc />
    public async Task NotifyCommunityAsync(Guid incidentId)
    {
        var spec = new BaseSpecification<Incident>(i => i.Id == incidentId);
        spec.ApplyInclude(q => q.Include(i => i.Medias));

        var incident = await _unitOfWork.Repository<Incident, Guid>().GetWithSpecAsync(spec, tracked: false);
        if (incident == null) return;

        var dto = new CommunityIncidentDto
        {
            Id = incident.Id,
            Code = incident.Code,
            // Masked coordinates (rounded to 3 decimal places for general area, ~110 meters accuracy)
            Latitude = Math.Round(incident.Location.Y, 3),
            Longitude = Math.Round(incident.Location.X, 3),
            AddressString = incident.AddressString,
            CurrentStatus = incident.CurrentStatus,
            PriorityLevel = incident.PriorityLevel,
            IsVerified = incident.CurrentAiReview != null && incident.CurrentAiReview.AdminReviewerId != null,
            CreatedAt = incident.CreatedAt,
            // Display incident image (Snake or Wound)
            IncidentImage = incident.Medias.OrderBy(m => m.MediaType).Select(m => m.MediaUrl).FirstOrDefault()
        };

        await _rescueHub.Clients.Group(DispatchConstants.AllRescuersGroup)
            .SendAsync(DispatchConstants.EventCommunityIncidentUpdated, dto);

        _logger.LogInformation("Broadcasted community update for IncidentId={Id}, Status={S}",
            incidentId, incident.CurrentStatus);
    }

    private async Task<bool> AnyAvailableRescuerWithinAsync(Point incidentLocation, double radiusMeters)
    {
        // Fail-fast MUST be a superset of all dispatch tiers. 
        // We use Tier3FreshnessHours (48h) so we don't prematurely fallback if a rescuer is eligible for Tier 3.
        var heartbeatCutoff = DateTime.UtcNow.AddHours(-DispatchConstants.Tier3FreshnessHours);

        var spec = new BaseSpecification<User>(u =>
            u.Status == UserStatus.Active &&
            u.RescuerProfile != null &&
            u.RescuerProfile.IsAvailable &&
            u.RescuerProfile.IsVerified &&
            u.CurrentLocation != null &&
            u.LocationUpdatedAt != null &&
            u.LocationUpdatedAt >= heartbeatCutoff &&
            u.CurrentLocation.Distance(incidentLocation) <= radiusMeters);

        return await _unitOfWork.Repository<User, Guid>().AnyAsync(spec);
    }

    private static bool IsFallbackEligibleStatus(IncidentStatus status)
        => status == IncidentStatus.Pending
        || status == IncidentStatus.Dispatching_Tier1
        || status == IncidentStatus.Dispatching_Tier2
        || status == IncidentStatus.Dispatching_Tier3;

    private static bool CanStartDispatch(IncidentStatus status)
        => status == IncidentStatus.Pending
        || status == IncidentStatus.Unassigned;
}
