using System;

namespace SFARS.Application.Dtos.Analytics;

/// <summary>Rescue mission assigned to an incident</summary>
public class SnakeMissionDetailDto
{
    /// <summary>Unique identifier of the rescue mission</summary>
    public Guid MissionId { get; set; }

    /// <summary>Unique identifier of the assigned rescuer</summary>
    public Guid RescuerId { get; set; }

    /// <summary>Full name of the rescuer</summary>
    public string RescuerName { get; set; } = null!;

    /// <summary>Current status of the mission (Pending, Accepted, Ongoing, Completed, etc.)</summary>
    public string MissionStatus { get; set; } = null!;

    /// <summary>When the mission was created/accepted (UTC)</summary>
    public DateTime MissionAcceptedAt { get; set; }

    /// <summary>When the rescuer started the rescue operation (UTC, nullable)</summary>
    public DateTime? MissionStartedAt { get; set; }

    /// <summary>When the rescuer arrived at the incident location (UTC, nullable)</summary>
    public DateTime? MissionArrivedAt { get; set; }

    /// <summary>When the rescue operation was completed (UTC, nullable)</summary>
    public DateTime? MissionCompletedAt { get; set; }

    /// <summary>Notes from the rescuer about the operation (nullable)</summary>
    public string? RescuerNotes { get; set; }

    /// <summary>Patient condition description at handover to medical facility (nullable)</summary>
    public string? PatientConditionAtHandover { get; set; }

    /// <summary>
    /// Response time in minutes: time from started to arrived.
    /// Calculated value: (ArrivedAt - StartedAt).TotalMinutes
    /// Returns 0 if either timestamp is missing.
    /// </summary>
    public double ResponseTimeMinutes { get; set; }

    /// <summary>
    /// Handover time in minutes: time spent at scene doing rescue.
    /// Calculated value: (CompletedAt - ArrivedAt).TotalMinutes
    /// Returns 0 if either timestamp is missing.
    /// </summary>
    public double HandoverTimeMinutes { get; set; }

    /// <summary>
    /// Total mission duration in minutes: from acceptance to completion.
    /// Calculated value: (CompletedAt - MissionAcceptedAt).TotalMinutes
    /// Returns 0 if CompletedAt is missing.
    /// </summary>
    public double TotalDurationMinutes { get; set; }
}
