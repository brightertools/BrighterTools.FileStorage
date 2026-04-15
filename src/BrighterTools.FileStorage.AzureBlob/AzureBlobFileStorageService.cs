using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using BrighterTools.FileStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BrighterTools.FileStorage.AzureBlob;

/// <summary>
/// Provides Azure Blob File Storage operations.
/// </summary>
public class AzureBlobFileStorageService : IFileStorageService
{
    private readonly ILogger<AzureBlobFileStorageService> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _defaultContainerName;
    private readonly string _storageAccountName;
    private readonly string _storageAccountKey;
    private readonly int _maxBlobPathLength;
    private readonly string _publicBaseUrl;
    private readonly ConcurrentDictionary<string, bool> _existingContainers = new();

    /// <summary>
    /// Executes Azure Blob File Storage Service.
    /// </summary>
    public AzureBlobFileStorageService(IOptions<AzureBlobStorageOptions> options, ILogger<AzureBlobFileStorageService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var config = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _blobServiceClient = new BlobServiceClient(config.ConnectionString);
        _defaultContainerName = config.ContainerName ?? throw new ArgumentNullException("AzureBlobStorage.ContainerName", "ContainerName cannot be null");
        _storageAccountName = config.AccountName ?? throw new ArgumentNullException("AzureBlobStorage.AccountName", "AccountName cannot be null");
        _storageAccountKey = config.AccountKey ?? throw new ArgumentNullException("AzureBlobStorage.AccountKey", "AccountKey cannot be null");
        _maxBlobPathLength = config.MaxBlobPathLength > 0 ? config.MaxBlobPathLength : 1000;
        _publicBaseUrl = config.PublicBaseUrl ?? string.Empty;
    }

    /// <summary>
    /// Deletes File.
    /// </summary>
    public Task<bool> DeleteFile(string relativePath) => DeleteFile(relativePath, _defaultContainerName);
    /// <summary>
    /// Gets File URL.
    /// </summary>
    public Task<string> GetFileUrl(string relativePath) => GetFileUrl(relativePath, _defaultContainerName);
    /// <summary>
    /// Executes File Exists.
    /// </summary>
    public Task<bool> FileExists(string relativePath) => FileExists(relativePath, _defaultContainerName);
    /// <summary>
    /// Executes Download File.
    /// </summary>
    public Task<byte[]> DownloadFile(string relativePath) => DownloadFile(relativePath, _defaultContainerName);
    /// <summary>
    /// Gets Signed URL.
    /// </summary>
    public Task<string> GetSignedUrl(string relativePath, TimeSpan expiryTime) => GetSignedUrl(relativePath, expiryTime, _defaultContainerName);
    /// <summary>
    /// Lists Files IN Folder.
    /// </summary>
    public Task<IReadOnlyList<StoredFileInfo>> ListFilesInFolder(string rootPath) => ListFilesInFolder(rootPath, _defaultContainerName);

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
    public async Task<(string RelativeUrl, string AbsoluteUrl)> UploadFile(string tenantIdentifier, string fileIdentifier, Stream fileStream, string fileName, string containerName, bool includeDefaultMetadata = true, Dictionary<string, string>? additionalMetadata = null, bool includeDefaultTags = true, Dictionary<string, string>? additionalTags = null, params string[] paths)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Filename cannot be empty", nameof(fileName));
        }

        await EnsureContainerExistsAsync(containerName);

        var extension = Path.GetExtension(fileName);
        var blobRelativePath = AzureBlobPathBuilder.GenerateSafePath(tenantIdentifier, fileIdentifier, fileName, _maxBlobPathLength, paths);
        var blobClient = GetBlobClient(blobRelativePath, containerName);

        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        var safeTimestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (includeDefaultMetadata)
        {
            metadata["uploaded"] = safeTimestamp;
        }

        if (additionalMetadata != null)
        {
            foreach (var kvp in additionalMetadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }
        }

        AzureBlobMetadataValidator.ValidateMetadata(metadata);

        var tags = new Dictionary<string, string>();
        if (includeDefaultTags && !string.IsNullOrWhiteSpace(tenantIdentifier))
        {
            tags["tenantid"] = SanitizeTagValue(tenantIdentifier);
            tags["ext"] = SanitizeTagValue(Path.GetExtension(fileName).TrimStart('.'));
            tags["name"] = SanitizeTagValue(fileName);
            tags["uploaded"] = safeTimestamp;
        }

        if (additionalTags != null)
        {
            foreach (var kvp in additionalTags)
            {
                tags[kvp.Key] = kvp.Value;
            }
        }

        AzureBlobTagValidator.ValidateTags(tags);

        var blobHttpHeaders = new BlobHttpHeaders
        {
            ContentType = MimeKit.MimeTypes.GetMimeType($"filename{extension}")
        };

        await blobClient.UploadAsync(memoryStream, new BlobUploadOptions
        {
            HttpHeaders = blobHttpHeaders,
            Metadata = metadata,
            Tags = tags
        });

        return (blobRelativePath, blobClient.Uri.AbsoluteUri);
    }

    /// <summary>
    /// Deletes File.
    /// </summary>
    public async Task<bool> DeleteFile(string relativePath, string containerName)
    {
        await EnsureContainerExistsAsync(containerName);
        var blobClient = GetBlobClient(relativePath, containerName);
        return await blobClient.DeleteIfExistsAsync();
    }

    /// <summary>
    /// Gets File URL.
    /// </summary>
    public async Task<string> GetFileUrl(string relativePath, string containerName)
    {
        await EnsureContainerExistsAsync(containerName);

        if (!string.IsNullOrWhiteSpace(_publicBaseUrl))
        {
            return AzureBlobUrlBuilder.BuildPublicUrl(_publicBaseUrl, containerName, relativePath);
        }

        return GetBlobClient(relativePath, containerName).Uri.ToString();
    }

    /// <summary>
    /// Executes File Exists.
    /// </summary>
    public async Task<bool> FileExists(string relativePath, string containerName)
    {
        await EnsureContainerExistsAsync(containerName);
        return await GetBlobClient(relativePath, containerName).ExistsAsync();
    }

    /// <summary>
    /// Executes Download File.
    /// </summary>
    public async Task<byte[]> DownloadFile(string relativePath, string containerName)
    {
        await EnsureContainerExistsAsync(containerName);

        var blobClient = GetBlobClient(relativePath, containerName);
        if (!await blobClient.ExistsAsync())
        {
            throw new FileNotFoundException($"File not found: {relativePath}");
        }

        using var ms = new MemoryStream();
        await blobClient.DownloadToAsync(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Executes Download File TO Path.
    /// </summary>
    public async Task DownloadFileToPath(string relativePath, string containerName, string destinationPath)
    {
        await EnsureContainerExistsAsync(containerName);

        var blobClient = GetBlobClient(relativePath, containerName);
        if (!await blobClient.ExistsAsync())
        {
            throw new FileNotFoundException($"File not found: {relativePath}");
        }

        await using var fileStream = File.OpenWrite(destinationPath);
        await blobClient.DownloadToAsync(fileStream);
    }

    /// <summary>
    /// Gets Signed URL.
    /// </summary>
    public async Task<string> GetSignedUrl(string relativePath, TimeSpan expiryTime, string containerName)
    {
        await EnsureContainerExistsAsync(containerName);

        var blobClient = GetBlobClient(relativePath, containerName);
        if (!await blobClient.ExistsAsync())
        {
            throw new FileNotFoundException($"File '{relativePath}' not found in {containerName}.");
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = relativePath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiryTime)
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var storageSharedKeyCredential = new StorageSharedKeyCredential(_storageAccountName, _storageAccountKey);
        var sasToken = sasBuilder.ToSasQueryParameters(storageSharedKeyCredential).ToString();
        return $"{blobClient.Uri}?{sasToken}";
    }

    /// <summary>
    /// Lists Files IN Folder.
    /// </summary>
    public async Task<IReadOnlyList<StoredFileInfo>> ListFilesInFolder(string rootPath, string containerName)
    {
        await EnsureContainerExistsAsync(containerName);

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobs = new List<StoredFileInfo>();

        if (!rootPath.EndsWith('/'))
        {
            rootPath += "/";
        }

        await foreach (var item in containerClient.GetBlobsByHierarchyAsync(BlobTraits.Metadata, BlobStates.None, "/", rootPath, CancellationToken.None))
        {
            if (!item.IsBlob || item.Blob == null)
            {
                continue;
            }

            var fileNameOnly = Path.GetFileName(item.Blob.Name);
            blobs.Add(new StoredFileInfo
            {
                StoredName = item.Blob.Name,
                DisplayName = RemoveGuidPrefix(fileNameOnly),
                LastModified = item.Blob.Properties.LastModified,
                Metadata = item.Blob.Metadata
            });
        }

        return blobs;
    }

    /// <summary>
    /// Gets Root URL.
    /// </summary>
    public async Task<string> GetRootUrl(string containerName)
    {
        await EnsureContainerExistsAsync(containerName);

        if (!string.IsNullOrWhiteSpace(_publicBaseUrl))
        {
            return $"{_publicBaseUrl.TrimEnd('/')}/{containerName}";
        }

        return _blobServiceClient.GetBlobContainerClient(containerName).Uri.ToString();
    }

    /// <summary>
    /// Copies File.
    /// </summary>
    public async Task CopyFile(string sourceContainerName, string sourceBlobPath, string destinationContainerName, string destinationBlobPath, TimeSpan? maxWait = null)
    {
        var sourceClient = GetBlobClient(sourceBlobPath, sourceContainerName);
        if (!await sourceClient.ExistsAsync())
        {
            throw new FileNotFoundException($"Source blob not found: container={sourceContainerName}, path={sourceBlobPath}");
        }

        await EnsureContainerExistsAsync(destinationContainerName);

        var destClient = GetBlobClient(destinationBlobPath, destinationContainerName);
        var copyResponse = await destClient.StartCopyFromUriAsync(sourceClient.Uri);

        _logger.LogInformation("Started copy from {SourceContainer}/{SourceBlob} to {DestinationContainer}/{DestinationBlob} with CopyId {CopyId}", sourceContainerName, sourceBlobPath, destinationContainerName, destinationBlobPath, copyResponse.Value);

        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            if (maxWait != null && stopwatch.Elapsed > maxWait)
            {
                throw new TimeoutException($"Copy operation did not complete within {maxWait.Value.TotalSeconds} seconds.");
            }

            await Task.Delay(500);

            var destProps = await destClient.GetPropertiesAsync();
            var status = destProps.Value.CopyStatus;
            if (status == CopyStatus.Pending)
            {
                continue;
            }

            if (status == CopyStatus.Success)
            {
                break;
            }

            var desc = destProps.Value.CopyStatusDescription;
            throw new IOException($"Blob copy failed: {status}. {desc}");
        }
    }

    /// <summary>
    /// Removes Tag.
    /// </summary>
    public async Task<bool> RemoveTagAsync(string relativePath, string containerName, string tagKeyToRemove)
    {
        if (string.IsNullOrWhiteSpace(tagKeyToRemove))
        {
            throw new ArgumentException("Tag key to remove cannot be null or empty.", nameof(tagKeyToRemove));
        }

        var blobClient = GetBlobClient(relativePath, containerName);
        if (!await blobClient.ExistsAsync())
        {
            throw new FileNotFoundException($"Blob not found: {relativePath} in container {containerName}");
        }

        var existingTags = (await blobClient.GetTagsAsync()).Value.Tags;
        if (!existingTags.ContainsKey(tagKeyToRemove))
        {
            _logger.LogInformation("Tag '{TagKey}' not found on blob '{BlobPath}'", tagKeyToRemove, relativePath);
            return false;
        }

        existingTags.Remove(tagKeyToRemove);
        var response = await blobClient.SetTagsAsync(existingTags);

        if (!response.IsError)
        {
            return true;
        }

        throw new IOException($"Failed to remove tag '{tagKeyToRemove}' from blob '{relativePath}': {response.Status}");
    }

    private async Task EnsureContainerExistsAsync(string containerName)
    {
        if (_existingContainers.ContainsKey(containerName))
        {
            return;
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync();
        _existingContainers.TryAdd(containerName, true);
    }

    private BlobClient GetBlobClient(string relativePath, string containerName) =>
        _blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(relativePath);

    private static string RemoveGuidPrefix(string filename) =>
        Regex.Replace(filename, @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}_", string.Empty);

    private static string SanitizeTagValue(string value) => Regex.Replace(value, @"[^A-Za-z0-9 \-._:/=+@]", "_");
}

