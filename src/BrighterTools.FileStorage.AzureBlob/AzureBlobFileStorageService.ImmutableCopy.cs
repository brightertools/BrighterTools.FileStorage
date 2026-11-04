using Azure;
using Azure.Storage.Blobs.Models;

namespace BrighterTools.FileStorage.AzureBlob;

public partial class AzureBlobFileStorageService : IImmutableObjectCopyStorage
{
    /// <inheritdoc />
    public long MaximumImmutableCopyBytes => 256L * 1024 * 1024;

    /// <inheritdoc />
    public async Task<bool> CopyImmutableAsync(string sourcePath, string sourceContainer, StorageObjectMetadata observed,
        string destinationPath, string destinationContainer, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTransferPath(sourcePath);
        ValidateTransferPath(destinationPath);
        ArgumentNullException.ThrowIfNull(observed);
        if (observed.Length <= 0 || observed.Length > MaximumImmutableCopyBytes)
            throw new ArgumentOutOfRangeException(nameof(observed));
        if (string.IsNullOrWhiteSpace(observed.ETag) || observed.ETag == "*")
            throw new ArgumentException("An exact observed source version is required.", nameof(observed));
        if (sourceContainer == destinationContainer && sourcePath == destinationPath)
            throw new ArgumentException("Source and destination must be different objects.");
        using var deadline = TransferDeadline(timeout, cancellationToken);
        deadline.Token.ThrowIfCancellationRequested();
        var source = _blobServiceClient.GetBlobContainerClient(sourceContainer).GetBlobClient(sourcePath);
        var destination = _blobServiceClient.GetBlobContainerClient(destinationContainer).GetBlobClient(destinationPath);
        try
        {
            var result = await destination.SyncCopyFromUriAsync(source.Uri, new BlobCopyFromUriOptions
            {
                SourceConditions = new BlobRequestConditions { IfMatch = new ETag(observed.ETag) },
                DestinationConditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
            }, deadline.Token);
            if (result.Value.CopyStatus != CopyStatus.Success)
                throw new InvalidDataException("Synchronous storage copy did not complete successfully.");
            return true;
        }
        catch (RequestFailedException e) when (e.Status == 412 || e.Status == 409 && e.ErrorCode == "BlobAlreadyExists")
        { return false; }
        catch (RequestFailedException e) when (e.Status == 404)
        { throw new FileNotFoundException("Copy source or destination container not found."); }
    }
}
