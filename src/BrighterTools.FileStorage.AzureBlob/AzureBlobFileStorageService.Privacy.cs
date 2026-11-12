using Azure.Storage.Blobs.Models;

namespace BrighterTools.FileStorage.AzureBlob;

public partial class AzureBlobFileStorageService : IPrivateFileStorage
{
    /// <inheritdoc />
    public async Task EnsurePrivateAsync(string container, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(container)) throw new ArgumentException("A container is required.", nameof(container));
        using var deadline = TransferDeadline(timeout, cancellationToken);
        deadline.Token.ThrowIfCancellationRequested();
        var properties = await _blobServiceClient.GetBlobContainerClient(container).GetPropertiesAsync(cancellationToken: deadline.Token);
        if (properties.Value.PublicAccess != PublicAccessType.None)
            throw new InvalidOperationException("File storage requires a private container.");
    }
}