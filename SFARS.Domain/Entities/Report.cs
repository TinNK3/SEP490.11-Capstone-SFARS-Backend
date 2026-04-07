using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class Report : BaseEntity
{
    /// <summary>
    /// ID of the user who created the report.
    /// Inherited from BaseEntity.CreatedBy but explicitly defined here for clarity if needed.
    /// Actually, BaseEntity already has CreatedBy (Guid?). 
    /// We'll use ReporterId as a formal FK for easier mapping.
    /// </summary>
    public Guid ReporterId { get; set; }
    
    /// <summary>
    /// The ID of the target content (Post or Reel).
    /// </summary>
    public Guid TargetId { get; set; }
    
    /// <summary>
    /// The type of target content (Post or Reel).
    /// </summary>
    public ReportTargetType TargetType { get; set; }
    
    /// <summary>
    /// The reason for reporting.
    /// </summary>
    public string Reason { get; set; } = null!;
    
    /// <summary>
    /// The current status of the report.
    /// </summary>
    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    
    /// <summary>
    /// The ID of the administrator who handled the report.
    /// </summary>
    public Guid? AdminId { get; set; }
    
    /// <summary>
    /// Admin's note or response to the report.
    /// </summary>
    public string? AdminNote { get; set; }

    // Navigation properties
    public virtual User Reporter { get; set; } = null!;
    public virtual User? Admin { get; set; }
}
