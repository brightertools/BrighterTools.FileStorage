using System.Runtime.CompilerServices;
using Azure;
using Azure.Storage.Blobs.Models;

namespace BrighterTools.FileStorage.AzureBlob;

public partial class AzureBlobFileStorageService
{
    /// <summary>Enumerates current blobs recursively without creating a container.</summary>
    public async IAsyncEnumerable<StoredFileInfo> EnumerateFiles(string rootPath, string containerName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || rootPath.StartsWith('/') || rootPath.Contains("..") || rootPath.Contains((char)92))
            throw new ArgumentException("An explicit relative inventory root is required.", nameof(rootPath));
        var prefix = rootPath.TrimEnd('/') + "/";
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        // Listing is a data-plane operation supported by a container-scoped SAS.
        // A preliminary container-properties request requires broader account authority.
        await using var items = container.GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, prefix, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool next;
            try { next = await items.MoveNextAsync(); }
            catch (RequestFailedException error) when (error.Status == 404 && error.ErrorCode == "ContainerNotFound") { next = false; }
            if (!next) yield break;
            var blob = items.Current;
            yield return new StoredFileInfo { StoredName = blob.Name, DisplayName = blob.Name,
                Metadata = blob.Metadata, LastModified = blob.Properties.LastModified };
        }
    }
}