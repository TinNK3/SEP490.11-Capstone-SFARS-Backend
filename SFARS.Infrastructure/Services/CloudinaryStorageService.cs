using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Infrastructure.Configurations;

namespace SFARS.Infrastructure.Services;

/// <summary>
/// Cloudinary implementation of file storage service
/// </summary>
public class CloudinaryStorageService : IFileStorageService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryStorageService> _logger;

    public CloudinaryStorageService(
        IOptions<CloudinarySettings> settings,
        ILogger<CloudinaryStorageService> logger)
    {
        _logger = logger;
        
        var account = new Account(
            settings.Value.CloudName,
            settings.Value.ApiKey,
            settings.Value.ApiSecret
        );
        
        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
    }

    /// <inheritdoc />
    public async Task<FileUploadResult> UploadAsync(Stream stream, string fileName, string folder, string contentType)
    {
        // Ensure stream is at the beginning
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        RawUploadParams uploadParams;

        if (contentType.StartsWith("image/"))
        {
            uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, stream),
                Folder = folder,
                Transformation = new Transformation()
                    .Quality("auto")
                    .FetchFormat("auto")
            };
        }
        else if (contentType.StartsWith("video/") || contentType.StartsWith("audio/"))
        {
            uploadParams = new VideoUploadParams
            {
                File = new FileDescription(fileName, stream),
                Folder = folder
            };
        }
        else
        {
            uploadParams = new RawUploadParams
            {
                File = new FileDescription(fileName, stream),
                Folder = folder
            };
        }

        try
        {
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                _logger.LogError("Cloudinary upload failed: {Error}", uploadResult.Error.Message);
                throw new InvalidOperationException($"FileStorage: Upload failed - {uploadResult.Error.Message}");
            }

            _logger.LogInformation("File uploaded successfully: {PublicId}", uploadResult.PublicId);

            return new FileUploadResult(
                uploadResult.SecureUrl.ToString(),
                uploadResult.PublicId,
                uploadResult.Bytes,
                uploadResult.Format
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to Cloudinary");
            throw new InvalidOperationException("FileStorage: Failed to upload file to cloud storage", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string publicId)
    {
        try
        {
            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams);

            if (result.Result == "ok")
            {
                _logger.LogInformation("File deleted successfully: {PublicId}", publicId);
                return true;
            }

            _logger.LogWarning("Failed to delete file: {PublicId}, Result: {Result}", publicId, result.Result);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from Cloudinary: {PublicId}", publicId);
            return false;
        }
    }
}