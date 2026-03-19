using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Common.Extensions;

public static class IncidentStatusExtensions
{
    /// <summary>
    /// An incident is trackable if it is not Closed or Cancelled.
    /// Uses negative check so new statuses are automatically trackable.
    /// </summary>
    public static bool IsTrackable(this IncidentStatus status)
        => status != IncidentStatus.Closed && status != IncidentStatus.Cancelled;
}