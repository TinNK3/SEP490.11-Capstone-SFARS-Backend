using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Dispatch;
using SFARS.Domain.Common;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;
using SFARS.Infrastructure.Hubs;

namespace SFARS.Application.Services;

public class MissionService : IMissionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobClient _jobs;
    private readonly IHubContext<LocationTrackingHub> _locationHub;
    private readonly ISystemMessageService _msgService;
    private readonly ILogger<MissionService> _logger;

    public MissionService(
        IUnitOfWork unitOfWork,
        IBackgroundJobClient jobs,
        IHubContext<LocationTrackingHub> locationHub,
        ISystemMessageService msgService,
        ILogger<MissionService> logger)
    {
        _unitOfWork = unitOfWork;
        _jobs = jobs;
        _locationHub = locationHub;
        _msgService = msgService;
        _logger = logger;
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
                @"UPDATE Incidents SET current_status = {0}, updated_at = {1}
                  WHERE id = {2} AND current_status IN ({3}, {4}, {5}, {6})",
                (int)IncidentStatus.Assigned, DateTime.UtcNow, incidentId,
                (int)IncidentStatus.Dispatching_Tier1, (int)IncidentStatus.Dispatching_Tier2,
                (int)IncidentStatus.Dispatching_Tier3, (int)IncidentStatus.Unassigned);
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

        // We won the race! Refresh status in memory if tracked
        incident.CurrentStatus = IncidentStatus.Assigned;
        incident.UpdatedAt = DateTime.UtcNow;

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
            mission);
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
        if (incident == null || incident.CurrentStatus != IncidentStatus.Assigned) return;

        // Has rescuer ghosted without refreshing location?
        // Check if the last location update is older than the configured timeout limit
        var timeSinceLastUpdate = DateTime.UtcNow - (mission.Rescuer.LocationUpdatedAt ?? mission.CreatedAt);
        
        if (timeSinceLastUpdate > TimeSpan.FromMinutes(DispatchConstants.ClaimTimeoutMinutes))
        {
            _logger.LogWarning("ClaimTimeout: Rescuer {RescuerId} ghosted Mission {MissionId}. Last update {Time} mins ago. Reverting...", mission.RescuerId, mission.Id, timeSinceLastUpdate.TotalMinutes);
            
            // Revert state
            mission.Status = RescueStatus.Rejected;
            mission.UpdatedAt = DateTime.UtcNow;

            incident.CurrentStatus = IncidentStatus.Unassigned;
            incident.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();

            // Notify victim that rescuer cancelled/timed out, searching... (could trigger RunTier1 again, but for now just fallback to Unassigned)
            var dto = new SosFallbackDto
            {
                IncidentId = incident.Id,
                Message = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Warning0002) // "Mission cancelled" (can be a better message)
            };
            await _locationHub.Clients
                .Group(LocationConstants.SignalRGroupPrefix + incident.Id)
                .SendAsync(DispatchConstants.EventFallback, dto);
            
            // Re-trigger dispatch?
            // To ensure reliability, we can enqueue StartDispatchAsync.
            _jobs.Enqueue<IDispatchService>(s => s.StartDispatchAsync(incident.Id));
        }
    }
}