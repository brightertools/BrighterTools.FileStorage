using System.Runtime.CompilerServices;
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
        if (!(await container.ExistsAsync(cancellationToken)).Value) yield break;
        await foreach (var blob in container.GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, prefix, cancellationToken))
            yield return new StoredFileInfo { StoredName = blob.Name, DisplayName = blob.Name,
                Metadata = blob.Metadata, LastModified = blob.Properties.LastModified };
    }
}