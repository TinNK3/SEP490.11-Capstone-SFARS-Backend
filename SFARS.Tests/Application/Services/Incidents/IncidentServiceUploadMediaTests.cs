using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Incident;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Infrastructure;

namespace SFARS.Tests.Application.Services.Incidents;

public class IncidentServiceUploadMediaTests : IncidentServiceTests
{
    #region UploadMediaAsync Tests

    [Fact]
    public async Task UploadMediaAsync_ValidRequest_ReturnsSuccessWithMediaDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        var fileName = "test-image.jpg";
        var contentType = "image/jpeg";
        var fileSize = 1024 * 1024; // 1MB
        var mediaType = MediaType.BiteWoundPhoto;

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        var uploadResult = new FileUploadResult(
            Url: "https://cloudinary.com/uploaded-image.jpg",
            PublicId: "incident-media/abc123",
            Size: fileSize,
            Format: "jpg"
        );

        using var stream = new MemoryStream(new byte[fileSize]);

        // Setup mocks
        _incidentRepoMock.Setup(x => x.GetByIdAsync(incidentId))
            .ReturnsAsync(incident);

        _fileStorageServiceMock.Setup(x => x.UploadAsync(
                stream, 
                fileName, 
                It.Is<string>(f => f.Contains(incidentId.ToString())),
                contentType))
            .ReturnsAsync(uploadResult);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _sut.UploadMediaAsync(
            userId, incidentId, stream, fileName, contentType, fileSize, mediaType);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        result.Data.Should().NotBeNull();
        
        var mediaDto = result.Data as IncidentMediaDto;
        mediaDto.Should().NotBeNull();
        mediaDto!.MediaUrl.Should().Be(uploadResult.Url);
        mediaDto.MediaType.Should().Be(mediaType);
        mediaDto.IncidentId.Should().Be(incidentId);

        // Verify storage service was called
        _fileStorageServiceMock.Verify(x => x.UploadAsync(
            stream, fileName, It.IsAny<string>(), contentType), Times.Once);
    }

    [Fact]
    public async Task UploadMediaAsync_EmptyUserId_ReturnsAuthWarning()
    {
        // Arrange
        var incidentId = Guid.NewGuid();
        using var stream = new MemoryStream();

        // Act
        var result = await _sut.UploadMediaAsync(
            Guid.Empty, incidentId, stream, "test.jpg", "image/jpeg", 1024, MediaType.BiteWoundPhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Auth_Warning0013);
        
        // Verify no storage operations occurred
        _fileStorageServiceMock.Verify(x => x.UploadAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadMediaAsync_EmptyIncidentId_ReturnsSysWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var stream = new MemoryStream();

        // Act
        var result = await _sut.UploadMediaAsync(
            userId, Guid.Empty, stream, "test.jpg", "image/jpeg", 1024, MediaType.BiteWoundPhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        
        _fileStorageServiceMock.Verify(x => x.UploadAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadMediaAsync_IncidentNotFound_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        using var stream = new MemoryStream();

        _incidentRepoMock.Setup(x => x.GetByIdAsync(incidentId))
            .ReturnsAsync((Incident?)null);

        // Act
        var result = await _sut.UploadMediaAsync(
            userId, incidentId, stream, "test.jpg", "image/jpeg", 1024, MediaType.BiteWoundPhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Incident_Warning0002);
        
        _fileStorageServiceMock.Verify(x => x.UploadAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadMediaAsync_UserNotOwner_ReturnsForbidden()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        using var stream = new MemoryStream();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = otherUserId, // Different user
            CurrentStatus = IncidentStatus.Pending
        };

        _incidentRepoMock.Setup(x => x.GetByIdAsync(incidentId))
            .ReturnsAsync(incident);

        // Act
        var result = await _sut.UploadMediaAsync(
            userId, incidentId, stream, "test.jpg", "image/jpeg", 1024, MediaType.BiteWoundPhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0007);
        
        _fileStorageServiceMock.Verify(x => x.UploadAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadMediaAsync_IncidentClosed_ReturnsWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        using var stream = new MemoryStream();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Closed
        };

        _incidentRepoMock.Setup(x => x.GetByIdAsync(incidentId))
            .ReturnsAsync(incident);

        // Act
        var result = await _sut.UploadMediaAsync(
            userId, incidentId, stream, "test.jpg", "image/jpeg", 1024, MediaType.BiteWoundPhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Incident_Warning0001);
        
        _fileStorageServiceMock.Verify(x => x.UploadAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadMediaAsync_IncidentCancelled_ReturnsWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        using var stream = new MemoryStream();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Cancelled
        };

        _incidentRepoMock.Setup(x => x.GetByIdAsync(incidentId))
            .ReturnsAsync(incident);

        // Act
        var result = await _sut.UploadMediaAsync(
            userId, incidentId, stream, "test.jpg", "image/jpeg", 1024, MediaType.BiteWoundPhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.Incident_Warning0001);
    }

    [Fact]
    public async Task UploadMediaAsync_FileSizeZero_ReturnsWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        using var stream = new MemoryStream();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        _incidentRepoMock.Setup(x => x.GetByIdAsync(incidentId))
            .ReturnsAsync(incident);

        // Act
        var result = await _sut.UploadMediaAsync(
            userId, incidentId, stream, "test.jpg", "image/jpeg", 0, MediaType.BiteWoundPhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        
        _fileStorageServiceMock.Verify(x => x.UploadAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadMediaAsync_FileTooLarge_ReturnsWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        var fileSizeTooLarge = 20 * 1024 * 1024; // 20MB (exceeds 10MB limit)
        using var stream = new MemoryStream();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        _incidentRepoMock.Setup(x => x.GetByIdAsync(incidentId))
            .ReturnsAsync(incident);

        // Act
        var result = await _sut.UploadMediaAsync(
            userId, incidentId, stream, "test.jpg", "image/jpeg", fileSizeTooLarge, MediaType.BiteWoundPhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0008);
        
        _fileStorageServiceMock.Verify(x => x.UploadAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("image/webp", MediaType.BiteWoundPhoto)] // Not in allowed list
    [InlineData("application/pdf", MediaType.SnakePhoto)]
    [InlineData("text/plain", MediaType.Other)]
    public async Task UploadMediaAsync_InvalidContentType_ReturnsWarning(string contentType, MediaType mediaType)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        using var stream = new MemoryStream();

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        _incidentRepoMock.Setup(x => x.GetByIdAsync(incidentId))
            .ReturnsAsync(incident);

        // Act
        var result = await _sut.UploadMediaAsync(
            userId, incidentId, stream, "test.file", contentType, 1024, mediaType);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
        
        _fileStorageServiceMock.Verify(x => x.UploadAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("image/jpeg", MediaType.BiteWoundPhoto)]
    [InlineData("image/jpg", MediaType.SnakePhoto)]
    [InlineData("image/png", MediaType.Other)]
    [InlineData("video/mp4", MediaType.Other)]
    public async Task UploadMediaAsync_ValidContentTypes_Success(string contentType, MediaType mediaType)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        var fileSize = 1024 * 1024;

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        var uploadResult = new FileUploadResult(
            "https://cloudinary.com/media.jpg",
            "media-id",
            fileSize,
            "jpg"
        );

        using var stream = new MemoryStream(new byte[fileSize]);

        _incidentRepoMock.Setup(x => x.GetByIdAsync(incidentId))
            .ReturnsAsync(incident);

        _fileStorageServiceMock.Setup(x => x.UploadAsync(
                It.IsAny<Stream>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(),
                contentType))
            .ReturnsAsync(uploadResult);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _sut.UploadMediaAsync(
            userId, incidentId, stream, "test.file", contentType, fileSize, mediaType);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        
        _fileStorageServiceMock.Verify(x => x.UploadAsync(
            stream, It.IsAny<string>(), It.IsAny<string>(), contentType), Times.Once);
    }

    [Fact]
    public async Task UploadMediaAsync_StorageServiceThrowsException_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        var fileSize = 1024;

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        using var stream = new MemoryStream(new byte[fileSize]);

        _incidentRepoMock.Setup(x => x.GetByIdAsync(incidentId))
            .ReturnsAsync(incident);

        _fileStorageServiceMock.Setup(x => x.UploadAsync(
                It.IsAny<Stream>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("FileStorage: Upload failed - Network error"));

        // Act
        var result = await _sut.UploadMediaAsync(
            userId, incidentId, stream, "test.jpg", "image/jpeg", fileSize, MediaType.BiteWoundPhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Fail0001);
        
        // Verify SaveChanges was never called
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UploadMediaAsync_DatabaseSaveFails_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        var fileSize = 1024;

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        var uploadResult = new FileUploadResult(
            "https://cloudinary.com/media.jpg",
            "media-id",
            fileSize,
            "jpg"
        );

        using var stream = new MemoryStream(new byte[fileSize]);

        _incidentRepoMock.Setup(x => x.GetByIdAsync(incidentId))
            .ReturnsAsync(incident);

        _fileStorageServiceMock.Setup(x => x.UploadAsync(
                It.IsAny<Stream>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(uploadResult);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ThrowsAsync(new DbUpdateException("Database error"));

        // Act
        var result = await _sut.UploadMediaAsync(
            userId, incidentId, stream, "test.jpg", "image/jpeg", fileSize, MediaType.BiteWoundPhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Fail0001);
    }

    [Fact]
    public async Task UploadMediaAsync_DatabaseReturnsZeroChanges_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        var fileSize = 1024;

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        var uploadResult = new FileUploadResult(
            "https://cloudinary.com/media.jpg",
            "media-id",
            fileSize,
            "jpg"
        );

        using var stream = new MemoryStream(new byte[fileSize]);

        _incidentRepoMock.Setup(x => x.GetByIdAsync(incidentId))
            .ReturnsAsync(incident);

        _fileStorageServiceMock.Setup(x => x.UploadAsync(
                It.IsAny<Stream>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(uploadResult);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(0); // No changes saved

        // Act
        var result = await _sut.UploadMediaAsync(
            userId, incidentId, stream, "test.jpg", "image/jpeg", fileSize, MediaType.BiteWoundPhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Fail0001);
    }

    [Fact]
    public async Task UploadMediaAsync_UsesConfiguredFolderFormat()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        var fileSize = 1024;

        var incident = new Incident
        {
            Id = incidentId,
            VictimId = userId,
            CurrentStatus = IncidentStatus.Pending
        };

        var uploadResult = new FileUploadResult(
            "https://cloudinary.com/media.jpg",
            "media-id",
            fileSize,
            "jpg"
        );

        using var stream = new MemoryStream(new byte[fileSize]);

        _incidentRepoMock.Setup(x => x.GetByIdAsync(incidentId))
            .ReturnsAsync(incident);

        _fileStorageServiceMock.Setup(x => x.UploadAsync(
                It.IsAny<Stream>(), 
                It.IsAny<string>(), 
                $"incidents/{incidentId}/media", // Expected folder from config
                It.IsAny<string>()))
            .ReturnsAsync(uploadResult);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _sut.UploadMediaAsync(
            userId, incidentId, stream, "test.jpg", "image/jpeg", fileSize, MediaType.BiteWoundPhoto);

        // Assert
        result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0001);
        
        // Verify folder format was used correctly
        _fileStorageServiceMock.Verify(x => x.UploadAsync(
            It.IsAny<Stream>(), 
            It.IsAny<string>(), 
            $"incidents/{incidentId}/media",
            It.IsAny<string>()), Times.Once);
    }

    #endregion
}