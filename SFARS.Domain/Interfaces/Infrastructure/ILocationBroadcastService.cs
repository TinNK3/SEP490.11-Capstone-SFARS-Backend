namespace SFARS.Domain.Interfaces.Infrastructure;

public interface ILocationBroadcastService
{
    /// <summary>
    /// Server-authoritative broadcast of user's location to all active incident groups.
    /// Throttled: max 1 broadcast per 2 seconds per user.
    /// </summary>
    Task BroadcastLocationToIncidentsAsync(Guid userId);
}