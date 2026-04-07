using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Models.Reports;

public class CreateReportModel
{
    public Guid TargetId { get; set; }
    public ReportTargetType TargetType { get; set; }
    public string Reason { get; set; } = null!;
}

public class UpdateReportStatusModel
{
    public ReportStatus Status { get; set; }
    public string? AdminNote { get; set; }
}
