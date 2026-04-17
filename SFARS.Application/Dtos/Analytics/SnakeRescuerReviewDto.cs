using System;

namespace SFARS.Application.Dtos.Analytics;

/// <summary>Rescuer's review/confirmation/correction of the AI identification</summary>
public class SnakeRescuerReviewDto
{
    /// <summary>Unique identifier of the rescuer who reviewed</summary>
    public Guid ReviewerId { get; set; }

    /// <summary>Full name of the rescuer</summary>
    public string ReviewerName { get; set; } = null!;

    /// <summary>Status of the review (ConfirmedCorrect, Corrected, UnableToAssess, AdminConfirmed, etc.)</summary>
    public string ReviewStatus { get; set; } = null!;

    /// <summary>
    /// If status is Corrected: the corrected snake common name.
    /// Otherwise null.
    /// </summary>
    public string? CorrectedSnakeName { get; set; }

    /// <summary>
    /// If status is Corrected: the corrected snake ID.
    /// Otherwise null.
    /// </summary>
    public Guid? CorrectedSnakeId { get; set; }

    /// <summary>Free-text note/comment from the rescuer (nullable)</summary>
    public string? ReviewComment { get; set; }

    /// <summary>When the review was submitted (UTC, nullable)</summary>
    public DateTime? ReviewedAt { get; set; }
}
