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
        if (incident is null || incident.CurrentStatus != IncidentStatus.Pending)
        {
            _logger.LogWarning("StartDispatchAsync skipped. IncidentId={Id} status={S}",
                incidentId, incident?.CurrentStatus);
            return;
        }

        var anyRescuer = await AnyAvailableRescuerWithinAsync(incident.Location, DispatchConstants.FailFastRadiusMeters);
        if (!anyRescuer)
        {
            _logger.LogWarning("Fail-fast: no available rescuer within {R} km for IncidentId={Id}",
                DispatchConstants.FailFastRadiusMeters / 1000, incidentId);
            await RunFallbackAsync(incidentId);
            return;
        }

        // Step 1: Update status to Tier 1 BEFORE enqueuing jobs to avoid race conditions
        incident.CurrentStatus = IncidentStatus.Dispatching_Tier1;
        incident.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        // Step 2: Enqueue/Schedule search tiers
        var j1 = _jobs.Enqueue<IDispatchService>(
            q => q.RunTier1Async(incidentId));

        var j2 = _jobs.Schedule<IDispatchService>(
            q => q.RunTier2Async(incidentId), DispatchConstants.Tier2Delay);

        var j3 = _jobs.Schedule<IDispatchService>(
            q => q.RunTier3Async(incidentId), DispatchConstants.Tier3Delay);

        var jf = _jobs.Schedule<IDispatchService>(
            q => q.RunFallbackAsync(incidentId), DispatchConstants.FallbackDelay);

        // Update with Job IDs for tracking/cancellation
        incident.DispatchJobIds = JsonSerializer.Serialize(new[] { j1, j2, j3, jf });
        await _unitOfWork.SaveChangesAsync();

        // Real-time: Notify all rescuers about new incident in community list
        await NotifyCommunityAsync(incidentId);

        _logger.LogInformation("Dispatch chain started for IncidentId={Id}. Status updated to Dispatching_Tier1. Jobs={Jobs}",
            incidentId, incident.DispatchJobIds);
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
        var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);

        if (incident is null || incident.CurrentStatus == IncidentStatus.Assigned || incident.CurrentStatus == IncidentStatus.Cancelled)
        {
            _logger.LogInformation("Fallback skipped — status is {S}. IncidentId={Id}", incident?.CurrentStatus, incidentId);
            return;
        }

        // Capture old status BEFORE mutation for accurate audit trail
        var oldStatus = incident.CurrentStatus;
        incident.CurrentStatus = IncidentStatus.Unassigned;
        incident.UpdatedAt = DateTime.UtcNow;

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

        await _unitOfWork.SaveChangesAsync();

        var message = await _msgService.GetMessageAsync(ResultCodeConst.Dispatch_Notify0002);
        var fallbackDto = new SosFallbackDto
        {
            IncidentId = incidentId,
            Message = message
        };

        await _locationHub.Clients
            .Group(LocationConstants.SignalRGroupPrefix + incidentId)
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
        var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);
        if (incident == null || incident.CurrentStatus != IncidentStatus.Unassigned)
        {
            return; // Already handled, assigned, or cancelled
        }

        _logger.LogWarning("Auto-closing abandoned incident {Id} after {H} hours of inactivity.", 
            incidentId, DispatchConstants.AbandonedIncidentExpiryHours);

        var oldStatus = incident.CurrentStatus;
        incident.CurrentStatus = IncidentStatus.Closed;
        incident.UpdatedAt = DateTime.UtcNow;

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
        
        var incident = await _unitOfWork.Repository<Incident, Guid>().GetWithSpecAsync(spec);

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
                incident.CurrentStatus = nextStatus.Value;
                incident.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();
            }
            return;
        }

        if (nextStatus.HasValue)
        {
            incident.CurrentStatus = nextStatus.Value;
            incident.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
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
                MinutesSinceBite = incident.MinutesSinceBite,
                ExtractedSymptoms = incident.ExtractedSymptoms
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
            CurrentStatus = incident.CurrentStatus,
            PriorityLevel = incident.PriorityLevel,
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
}