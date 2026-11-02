namespace BrighterTools.FileStorage;

/// <summary>Optional streaming recursive inventory; existing folder-list behavior is unchanged.</summary>
public interface IFileStorageInventory
{
    IAsyncEnumerable<StoredFileInfo> EnumerateFiles(string rootPath, string containerName, CancellationToken cancellationToken = default);
}