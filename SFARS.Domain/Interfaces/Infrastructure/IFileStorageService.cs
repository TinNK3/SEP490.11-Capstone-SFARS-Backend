namespace SFARS.Domain.Interfaces.Infrastructure;

/// <summary>
/// Interface for cloud file storage operations
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Upload a file to cloud storage
    /// </summary>
    /// <param name="stream">File stream</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="folder">Folder/path in storage</param>
    /// <param name="contentType">MIME type of the file</param>
    /// <returns>Upload result with URL and public ID</returns>
    Task<FileUploadResult> UploadAsync(Stream stream, string fileName, string folder, string contentType);

    /// <summary>
    /// Delete a file from cloud storage
    /// </summary>
    /// <param name="publicId">Public ID of the file</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteAsync(string publicId);

    /// <summary>
    /// Delete a file from cloud storage by its full URL
    /// </summary>
    /// <param name="url">Full URL of the file</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteByUrlAsync(string url);
}

/// <summary>
/// Result of a file upload operation
/// </summary>
public record FileUploadResult(
    string Url,
    string PublicId,
    long Size,
    string Format
);