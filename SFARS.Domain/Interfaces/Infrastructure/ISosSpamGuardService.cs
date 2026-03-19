using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Interfaces.Infrastructure;

/// <summary>
/// Redis-backed guard against SOS spam (repeated trigger-and-cancel).
/// Returns structured result so calling code can decide how to respond.
/// </summary>
public interface ISosSpamGuardService
{
    /// <summary>
    /// Check whether the user is allowed to create a new SOS incident.
    /// </summary>
    /// <returns>
    /// <see cref="SosSpamStatus.Allowed"/> — proceed normally.<br/>
    /// <see cref="SosSpamStatus.SoftWarning"/> — allowed but FE should show a warning.<br/>
    /// <see cref="SosSpamStatus.HardBlocked"/> — reject the request.
    /// </returns>
    Task<SosSpamStatus> CheckAsync(Guid userId);

    /// <summary>
    /// Record a cancellation event for the user (called when a Pending SOS is cancelled).
    /// </summary>
    Task RecordCancellationAsync(Guid userId);

    /// <summary>
    /// Remove the hard-block for a user (e.g., admin override or block expired).
    /// </summary>
    Task ClearBlockAsync(Guid userId);
}