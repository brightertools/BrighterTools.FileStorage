namespace BrighterTools.FileStorage;

/// <summary>Optional create-only, bounded streaming upload capability. Hosts own authorization and MIME validation.</summary>
public interface IWritableFileStorage
{
    Task<StorageObjectMetadata> CreateAsync(string path, string container, Stream source, string contentType, long maximumBytes, CancellationToken cancellationToken = default);
}
