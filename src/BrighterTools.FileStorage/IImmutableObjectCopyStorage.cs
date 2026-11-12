namespace BrighterTools.FileStorage;

/// <summary>Version-pinned, create-only copies within a provider account, without client upload.</summary>
public interface IImmutableObjectCopyStorage
{
    /// <summary>Largest object supported by this provider's synchronous copy operation.</summary>
    long MaximumImmutableCopyBytes { get; }

    /// <summary>
    /// Copies the exact observed source version, preserving its content properties.
    /// False means a source/destination precondition prevented creation. The caller must
    /// verify any existing destination independently. Never overwrites or deletes either object.
    /// Both containers must already exist. The caller owns privacy checks and checksum verification.
    /// </summary>
    Task<bool> CopyImmutableAsync(string sourcePath, string sourceContainer, StorageObjectMetadata observed,
        string destinationPath, string destinationContainer, TimeSpan timeout, CancellationToken cancellationToken = default);
}
