using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Report;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Models.Reports;
using SFARS.Domain.Specifications;

namespace SFARS.Application.Services;

public class ReportService : IReportService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ReportService> _logger;

    public ReportService(IUnitOfWork uow, ILogger<ReportService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<IServiceResult> CreateReportAsync(Guid reporterId, CreateReportModel request)
    {
        // 1. Verify target exists
        bool exists = false;
        if (request.TargetType == ReportTargetType.Post)
        {
            exists = await _uow.Repository<ContentPost, Guid>().AnyAsync(p => p.Id == request.TargetId);
        }
        else if (request.TargetType == ReportTargetType.Reel)
        {
            exists = await _uow.Repository<Reel, Guid>().AnyAsync(r => r.Id == request.TargetId);
        }

        if (!exists)
        {
            return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Nội dung bị báo cáo không tồn tại.");
        }

        // 2. Create report
        var report = new Report
        {
            ReporterId = reporterId,
            TargetId = request.TargetId,
            TargetType = request.TargetType,
            Reason = request.Reason,
            Status = ReportStatus.Pending,
            CreatedBy = reporterId
        };

        await _uow.Repository<Report, Guid>().AddAsync(report);
        await _uow.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Gửi báo cáo thành công.", report.Adapt<ReportResponse>());
    }

    public async Task<IServiceResult> GetMyReportsAsync(Guid userId)
    {
        var spec = new BaseSpecification<Report>(r => r.ReporterId == userId);
        spec.ApplyInclude(q => q.Include(r => r.Reporter));
        spec.AddOrderByDescending(r => r.CreatedAt);

        var reports = await _uow.Repository<Report, Guid>().GetAllWithSpecAsync(spec, tracked: false);
        return new ServiceResult(ResultCodeConst.SYS_Success0002, "Lấy danh sách báo cáo thành công.", reports.Adapt<List<ReportResponse>>());
    }

    public async Task<IServiceResult> GetAllReportsAsync()
    {
        var spec = new BaseSpecification<Report>(r => true);
        spec.ApplyInclude(q => q.Include(r => r.Reporter));
        spec.ApplyInclude(q => q.Include(r => r.Admin)!);
        spec.AddOrderByDescending(r => r.CreatedAt);

        var reports = await _uow.Repository<Report, Guid>().GetAllWithSpecAsync(spec, tracked: false);
        return new ServiceResult(ResultCodeConst.SYS_Success0002, "Lấy danh sách báo cáo thành công.", reports.Adapt<List<ReportResponse>>());
    }

    public async Task<IServiceResult> UpdateReportStatusAsync(Guid adminId, Guid reportId, UpdateReportStatusModel request)
    {
        var report = await _uow.Repository<Report, Guid>().GetByIdAsync(reportId);
        if (report == null)
        {
            return new ServiceResult(ResultCodeConst.SYS_Warning0004, "Báo cáo không tồn tại.");
        }

        report.Status = request.Status;
        report.AdminNote = request.AdminNote;
        report.AdminId = adminId;
        report.UpdatedAt = DateTime.UtcNow;
        report.UpdatedBy = adminId;

        _uow.Repository<Report, Guid>().Update(report);
        await _uow.SaveChangesAsync();

        return new ServiceResult(ResultCodeConst.SYS_Success0001, "Cập nhật trạng thái báo cáo thành công.", report.Adapt<ReportResponse>());
    }
}
