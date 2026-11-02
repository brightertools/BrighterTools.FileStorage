namespace BrighterTools.FileStorage;

/// <summary>Optional, non-buffering storage reads for HTTP delivery. Hosts own authorization.</summary>
public interface IStreamingFileStorage
{
    /// <summary>Reads version, length and content metadata without downloading the object.</summary>
    Task<StorageObjectMetadata> ReadMetadataAsync(string path, string container, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>Opens exactly the requested bytes of the observed version. Caller owns disposal.</summary>
    Task<Stream> OpenReadRangeAsync(string path, string container, StorageObjectMetadata observed,
        long offset, long length, TimeSpan idleTimeout, CancellationToken cancellationToken = default);
}

/// <summary>Internal storage metadata, not a public API response or authorization decision.</summary>
public sealed record StorageObjectMetadata(long Length, string ETag, DateTimeOffset LastModified, string ContentType);

