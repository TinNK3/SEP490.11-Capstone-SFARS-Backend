using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

/// <summary>
/// Audit trail for tracking all changes to Snake master data.
/// Each row = one field change for one snake.
/// </summary>
public class SnakeChangeLog : BaseEntity
{
    public Guid SnakeId { get; set; }

    /// <summary>
    /// The name of the field that was changed (e.g. "ToxinGroup", "CommonName")
    /// </summary>
    public string FieldName { get; set; } = null!;

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    /// <summary>
    /// Type of change: "Create", "Update", "SoftDelete", "Restore", "Import"
    /// </summary>
    public string ChangeType { get; set; } = null!;

    /// <summary>
    /// Mandatory for sensitive fields (ToxinGroup, ToxicityLevel). Optional otherwise.
    /// </summary>
    public string? ChangeReason { get; set; }

    // Navigation
    public virtual Snake Snake { get; set; } = null!;
}