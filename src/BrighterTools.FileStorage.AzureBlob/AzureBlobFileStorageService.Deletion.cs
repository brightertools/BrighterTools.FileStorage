using Azure;
using Azure.Storage.Blobs.Models;

namespace BrighterTools.FileStorage.AzureBlob;

public partial class AzureBlobFileStorageService : IConditionalDeleteStorage
{
    /// <inheritdoc />
    public async Task<bool> DeleteObservedAsync(string path, string container, StorageObjectMetadata observed,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTransferPath(path);
        ArgumentNullException.ThrowIfNull(observed);
        if (string.IsNullOrWhiteSpace(observed.ETag) || observed.ETag == "*")
            throw new ArgumentException("An exact observed version is required.", nameof(observed));
        using var deadline = TransferDeadline(timeout, cancellationToken);
        deadline.Token.ThrowIfCancellationRequested();
        var blob = _blobServiceClient.GetBlobContainerClient(container).GetBlobClient(path);
        try
        {
            var properties = (await blob.GetPropertiesAsync(cancellationToken: deadline.Token)).Value;
            if (properties.Metadata.TryGetValue(ProtocolMetadata, out var protocol) && protocol == SealedProtocol)
                throw new InvalidOperationException("Permanent storage seals cannot be deleted.");
            if (properties.ETag.ToString() != observed.ETag)
                throw new IOException("The stored object changed before deletion.");
            return (await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.None,
                new BlobRequestConditions { IfMatch = new ETag(observed.ETag) }, deadline.Token)).Value;
        }
        catch (RequestFailedException e) when (e.Status == 404) { return false; }
    }
}
