using System.Text.Json;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

/// <summary>
/// Transactional Outbox pattern implementation.
/// Ensures that side-effects (notifications, re-dispatching) are executed 
/// atomically with the database transactions.
/// </summary>
public class OutboxMessage : BaseEntity
{
    /// <summary>
    /// The type of event (e.g., "SendMissionTimeoutNotification", "IncidentDispatchRequested").
    /// </summary>
    public string EventType { get; set; } = null!;

    /// <summary>
    /// The serialized JSON payload for the event.
    /// </summary>
    public string Payload { get; set; } = null!;

    /// <summary>
    /// Whether the message has been processed by the background worker.
    /// </summary>
    public bool IsProcessed { get; set; } = false;

    /// <summary>
    /// When the message was processed.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Error message if processing fails.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; } = 0;
}