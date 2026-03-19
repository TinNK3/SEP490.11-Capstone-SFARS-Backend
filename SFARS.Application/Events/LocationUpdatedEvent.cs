using MediatR;

namespace SFARS.Application.Events;

/// <summary>
/// Published after a user's location is persisted to DB.
/// Handlers: cache write + SignalR broadcast.
/// </summary>
public record LocationUpdatedEvent(
    Guid UserId,
    double Latitude,
    double Longitude,
    DateTime UpdatedAt,
    double? AccuracyMeters) : INotification;