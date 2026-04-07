using FluentAssertions;
using Mapster;
using Microsoft.Extensions.Logging;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Report;
using SFARS.Application.Services;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Models.Reports;
using SFARS.Domain.Specifications.Interfaces;
using System.Linq.Expressions;

namespace SFARS.Tests.Application.Services;

public class ReportServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<ILogger<ReportService>> _loggerMock;
    private readonly Mock<IGenericRepository<Report, Guid>> _reportRepoMock;
    private readonly Mock<IGenericRepository<ContentPost, Guid>> _postRepoMock;
    private readonly Mock<IGenericRepository<Reel, Guid>> _reelRepoMock;
    private readonly ReportService _sut;

    public ReportServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<ReportService>>();
        _reportRepoMock = new Mock<IGenericRepository<Report, Guid>>();
        _postRepoMock = new Mock<IGenericRepository<ContentPost, Guid>>();
        _reelRepoMock = new Mock<IGenericRepository<Reel, Guid>>();

        _uowMock.Setup(x => x.Repository<Report, Guid>()).Returns(_reportRepoMock.Object);
        _uowMock.Setup(x => x.Repository<ContentPost, Guid>()).Returns(_postRepoMock.Object);
        _uowMock.Setup(x => x.Repository<Reel, Guid>()).Returns(_reelRepoMock.Object);

        _sut = new ReportService(_uowMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateReportAsync_TargetExists_ReturnsSuccess()
    {
        // Arrange
        var reporterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var request = new CreateReportModel 
        { 
            TargetId = targetId, 
            TargetType = ReportTargetType.Post, 
            Reason = "Spam" 
        };

        _postRepoMock.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<ContentPost, bool>>>()))
            .ReturnsAsync(true);

        _reportRepoMock.Setup(r => r.AddAsync(It.IsAny<Report>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateReportAsync(reporterId, request);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        _reportRepoMock.Verify(r => r.AddAsync(It.Is<Report>(rp => rp.Reason == "Spam")), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateReportAsync_TargetNotFound_ReturnsWarning()
    {
        // Arrange
        var reporterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var request = new CreateReportModel { TargetId = targetId, TargetType = ReportTargetType.Reel };

        _reelRepoMock.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Reel, bool>>>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.CreateReportAsync(reporterId, request);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        _reportRepoMock.Verify(r => r.AddAsync(It.IsAny<Report>()), Times.Never);
    }

    [Fact]
    public async Task GetMyReportsAsync_ReturnsUserReports()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reports = new List<Report>
        {
            new Report { Id = Guid.NewGuid(), ReporterId = userId, Reason = "Reason 1" },
            new Report { Id = Guid.NewGuid(), ReporterId = userId, Reason = "Reason 2" }
        };

        _reportRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Report>>(), false))
            .ReturnsAsync(reports);

        // Act
        var result = await _sut.GetMyReportsAsync(userId);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
        var data = result.Data as List<ReportResponse>;
        data.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateReportStatusAsync_ReportExists_UpdatesSuccessfully()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var report = new Report { Id = reportId, Status = ReportStatus.Pending };
        var request = new UpdateReportStatusModel { Status = ReportStatus.Resolved, AdminNote = "Fixed" };

        _reportRepoMock.Setup(r => r.GetByIdAsync(reportId)).ReturnsAsync(report);

        // Act
        var result = await _sut.UpdateReportStatusAsync(adminId, reportId, request);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        report.Status.Should().Be(ReportStatus.Resolved);
        report.AdminNote.Should().Be("Fixed");
        report.AdminId.Should().Be(adminId);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateReportStatusAsync_ReportNotFound_ReturnsWarning()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        _reportRepoMock.Setup(r => r.GetByIdAsync(reportId)).ReturnsAsync((Report?)null);

        // Act
        var result = await _sut.UpdateReportStatusAsync(adminId, reportId, new UpdateReportStatusModel());

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0004);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }
}
