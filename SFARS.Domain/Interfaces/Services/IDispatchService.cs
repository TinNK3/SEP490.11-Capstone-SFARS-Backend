namespace SFARS.Domain.Interfaces.Services;

/// <summary>
/// Orchestrates the tiered SOS dispatch pipeline.
/// Entry point: StartDispatchAsync — called by AnalyzeAsync after saving GraceExpiresAt.
/// Hang­fire jobs call the individual tier methods with a scheduled delay.
/// </summary>
public interface IDispatchService
{
    /// <summary>
    /// Initiates the dispatch pipeline for an incident:
    /// 1. Fail-fast check (20 km radius — if no rescuer exists, jump to fallback).
    /// 2. Schedules the Hangfire job chain (Tier1 → Tier2 → Tier3 → Fallback).
    /// </summary>
    Task StartDispatchAsync(Guid incidentId);

    /// <summary>Tier-1 sweep (0 s): radius 5 km / ETA 15 min.</summary>
    Task RunTier1Async(Guid incidentId);

    /// <summary>Tier-2 sweep (30 s): radius 10 km / ETA 30 min.</summary>
    Task RunTier2Async(Guid incidentId);

    /// <summary>Tier-3 sweep (60 s): radius 20 km / ETA 45 min.</summary>
    Task RunTier3Async(Guid incidentId);

    /// <summary>
    /// Triggered after 90 s if nobody has accepted.
    /// Sets status to Unassigned and pushes sos:fallback to the victim.
    /// Incident remains open so any rescuer who comes online can still self-assign.
    /// Schedules an automatic closure if no activity is recorded.
    /// </summary>
    Task RunFallbackAsync(Guid incidentId);

    /// <summary>
    /// Automatically closes an incident that has been Unassigned for a long time.
    /// This prevents "Ghost SOS" incidents from cluttering the system.
    /// </summary>
    Task AutoCloseAbandonedIncidentAsync(Guid incidentId);

    /// <summary>
    /// Broadcasts a community-wide update for an incident to all connected rescuers.
    /// Used when an incident becomes public (Dispatching/Unassigned) or is claimed (Assigned).
    /// </summary>
    Task NotifyCommunityAsync(Guid incidentId);
}