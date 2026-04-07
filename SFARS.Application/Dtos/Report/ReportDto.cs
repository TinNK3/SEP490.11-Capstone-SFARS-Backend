using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Report;

public class CreateReportRequest
{
    public Guid TargetId { get; set; }
    public ReportTargetType TargetType { get; set; }
    public string Reason { get; set; } = null!;
}

public class UpdateReportStatusRequest
{
    public ReportStatus Status { get; set; }
    public string? AdminNote { get; set; }
}

public class ReportResponse
{
    public Guid Id { get; set; }
    public Guid ReporterId { get; set; }
    public string ReporterName { get; set; } = null!;
    public Guid TargetId { get; set; }
    public ReportTargetType TargetType { get; set; }
    public string Reason { get; set; } = null!;
    public ReportStatus Status { get; set; }
    public Guid? AdminId { get; set; }
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }
}
