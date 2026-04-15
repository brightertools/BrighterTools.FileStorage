namespace BrighterTools.FileStorage;

/// <summary>
/// Defines operations for File Storage Service.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Uploads the upload File.
    /// </summary>
    /// <param name="tenantIdentifier">The tenantIdentifier value.</param>
    /// <param name="fileIdentifier">The fileIdentifier value.</param>
    /// <param name="fileStream">The fileStream value.</param>
    /// <param name="fileName">The fileName value.</param>
    /// <param name="containerName">The containerName value.</param>
    /// <param name="includeDefaultMetadata">The includeDefaultMetadata value.</param>
    /// <param name="additionalMetadata">The additionalMetadata value.</param>
    /// <param name="includeDefaultTags">The includeDefaultTags value.</param>
    /// <param name="additionalTags">The additionalTags value.</param>
    /// <param name="paths">The paths value.</param>
    /// <returns>A task that represents the asynchronous operation and contains the operation result.</returns>
    Task<(string RelativeUrl, string AbsoluteUrl)> UploadFile(string tenantIdentifier, string fileIdentifier, Stream fileStream, string fileName, string containerName, bool includeDefaultMetadata = true, Dictionary<string, string>? additionalMetadata = null, bool includeDefaultTags = true, Dictionary<string, string>? additionalTags = null, params string[] paths);
    /// <summary>
    /// Deletes the delete File.
    /// </summary>
    /// <param name="relativePath">The relativePath value.</param>
    /// <returns>A task that represents the asynchronous operation and contains the operation result.</returns>
    Task<bool> DeleteFile(string relativePath);
    /// <summary>
    /// Deletes the delete File.
    /// </summary>
    /// <param name="relativePath">The relativePath value.</param>
    /// <param name="containerName">The containerName value.</param>
    /// <returns>A task that represents the asynchronous operation and contains the operation result.</returns>
    Task<bool> DeleteFile(string relativePath, string containerName);
    /// <summary>
    /// Gets the get File Url.
    /// </summary>
    /// <param name="relativePath">The relativePath value.</param>
    /// <returns>A task that represents the asynchronous operation and contains the operation result.</returns>
    Task<string> GetFileUrl(string relativePath);
    /// <summary>
    /// Gets the get File Url.
    /// </summary>
    /// <param name="relativePath">The relativePath value.</param>
    /// <param name="containerName">The containerName value.</param>
    /// <returns>A task that represents the asynchronous operation and contains the operation result.</returns>
    Task<string> GetFileUrl(string relativePath, string containerName);
    /// <summary>
    /// Executes the file Exists operation.
    /// </summary>
    /// <param name="relativePath">The relativePath value.</param>
    /// <returns>A task that represents the asynchronous operation and contains the operation result.</returns>
    Task<bool> FileExists(string relativePath);
    /// <summary>
    /// Executes the file Exists operation.
    /// </summary>
    /// <param name="relativePath">The relativePath value.</param>
    /// <param name="containerName">The containerName value.</param>
    /// <returns>A task that represents the asynchronous operation and contains the operation result.</returns>
    Task<bool> FileExists(string relativePath, string containerName);
    /// <summary>
    /// Downloads the download File.
    /// </summary>
    /// <param name="relativePath">The relativePath value.</param>
    /// <returns>A task that represents the asynchronous operation and contains the operation result.</returns>
    Task<byte[]> DownloadFile(string relativePath);
    /// <summary>
    /// Downloads the download File.
    /// </summary>
    /// <param name="relativePath">The relativePath value.</param>
    /// <param name="containerName">The containerName value.</param>
    /// <returns>A task that represents the asynchronous operation and contains the operation result.</returns>
    Task<byte[]> DownloadFile(string relativePath, string containerName);
    /// <summary>
    /// Downloads the download File To Path.
    /// </summary>
    /// <param name="relativePath">The relativePath value.</param>
    /// <param name="containerName">The containerName value.</param>
    /// <param name="destinationPath">The destinationPath value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DownloadFileToPath(string relativePath, string containerName, string destinationPath);
    /// <summary>
    /// Gets the get Signed Url.
    /// </summary>
    /// <param name="relativePath">The relativePath value.</param>
    /// <param name="expiryTime">The expiryTime value.</param>
    /// <returns>A task that represents the asynchronous operation and contains the operation result.</returns>
    Task<string> GetSignedUrl(string relativePath, TimeSpan expiryTime);
    /// <summary>
    /// Gets the get Signed Url.
    /// </summary>
    /// <param name="relativePath">The relativePath value.</param>
    /// <param name="expiryTime">The expiryTime value.</param>
    /// <param name="containerName">The containerName value.</param>
    /// <returns>A task that represents the asynchronous operation and contains the operation result.</returns>
    Task<string> GetSignedUrl(string relativePath, TimeSpan expiryTime, string containerName);
    /// <summary>
    /// Lists the list Files In Folder.
    /// </summary>
    /// <param name="rootPath">The rootPath value.</param>
    /// <returns>A task that represents the asynchronous operation and contains the operation result.</returns>
    Task<IReadOnlyList<StoredFileInfo>> ListFilesInFolder(string rootPath);
    /// <summary>
    /// Lists the list Files In Folder.
    /// </summary>
    /// <param name="rootPath">The rootPath value.</param>
    /// <param name="containerName">The containerName value.</param>
    /// <returns>A task that represents the asynchronous operation and contains the operation result.</returns>
    Task<IReadOnlyList<StoredFileInfo>> ListFilesInFolder(string rootPath, string containerName);
    /// <summary>
    /// Gets the get Root Url.
    /// </summary>
    /// <param name="containerName">The containerName value.</param>
    /// <returns>A task that represents the asynchronous operation and contains the operation result.</returns>
    Task<string> GetRootUrl(string containerName);
    /// <summary>
    /// Copies the copy File.
    /// </summary>
    /// <param name="sourceContainerName">The sourceContainerName value.</param>
    /// <param name="sourceBlobPath">The sourceBlobPath value.</param>
    /// <param name="destinationContainerName">The destinationContainerName value.</param>
    /// <param name="destinationBlobPath">The destinationBlobPath value.</param>
    /// <param name="maxWait">The maxWait value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CopyFile(string sourceContainerName, string sourceBlobPath, string destinationContainerName, string destinationBlobPath, TimeSpan? maxWait = null);
    /// <summary>
    /// Removes the remove Tag Async.
    /// </summary>
    /// <param name="relativePath">The relativePath value.</param>
    /// <param name="containerName">The containerName value.</param>
    /// <param name="tagKeyToRemove">The tagKeyToRemove value.</param>
    /// <returns>A task that represents the asynchronous operation and contains the operation result.</returns>
    Task<bool> RemoveTagAsync(string relativePath, string containerName, string tagKeyToRemove);
}

