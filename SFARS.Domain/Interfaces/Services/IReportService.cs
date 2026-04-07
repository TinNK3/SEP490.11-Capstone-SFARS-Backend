using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Models.Reports;

namespace SFARS.Domain.Interfaces.Services;

public interface IReportService
{
    Task<IServiceResult> CreateReportAsync(Guid reporterId, CreateReportModel request);
    Task<IServiceResult> GetMyReportsAsync(Guid userId);
    Task<IServiceResult> GetAllReportsAsync();
    Task<IServiceResult> UpdateReportStatusAsync(Guid adminId, Guid reportId, UpdateReportStatusModel request);
}
