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

    // Use a static instance to prevent HttpClient socket exhaustion and connection drops
    private static Cloudinary? _cloudinaryInstance;
    private static readonly object _lock = new();

    public CloudinaryStorageService(
        IOptions<CloudinarySettings> settings,
        ILogger<CloudinaryStorageService> logger)
    {
        _logger = logger;

        if (_cloudinaryInstance == null)
        {
            lock (_lock)
            {
                if (_cloudinaryInstance == null)
                {
                    var account = new Account(
                        settings.Value.CloudName,
                        settings.Value.ApiKey,
                        settings.Value.ApiSecret
                    );
                    
                    _cloudinaryInstance = new Cloudinary(account);
                    _cloudinaryInstance.Api.Secure = true;
                }
            }
        }
        
        _cloudinary = _cloudinaryInstance;
    }

    /// <inheritdoc />
    public async Task<FileUploadResult> UploadAsync(Stream stream, string fileName, string folder, string contentType)
    {
        // Ensure stream is at the beginning
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        // Sanitize the filename. Non-ASCII characters (e.g., Vietnamese) in Multipart headers 
        // can cause WAF/Cloudinary to forcefully close the connection (IOException/SocketException 10054).
        string safeFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(fileName);
        string tempFilePath = Path.Combine(Path.GetTempPath(), safeFileName);

        try
        {
            // Write stream to a physical temp file. This forces CloudinaryDotNet's HttpClient to send an explicit 
            // Content-Length header rather than using Transfer-Encoding: chunked for MemoryStreams. 
            // Chunked sending is notoriously blocked or infinitely buffered by strict local Windows antiviruses/proxies
            // leading to the 100-second TaskCanceledException (Timeout) you are experiencing.
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await stream.CopyToAsync(fileStream);
            }

            // At this point, the file is fully dumped to disk and its size is perfectly known.
            var fileDesc = new FileDescription(tempFilePath);

            RawUploadParams uploadParams;

            if (contentType.StartsWith("image/"))
            {
                uploadParams = new ImageUploadParams
                {
                    File = fileDesc,
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
                    File = fileDesc,
                    Folder = folder
                };
            }
            else
            {
                uploadParams = new RawUploadParams
                {
                    File = fileDesc,
                    Folder = folder
                };
            }

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
        finally
        {
            // Clean up the temporary file
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Failed to delete temporary file {TempFilePath}", tempFilePath);
                }
            }
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