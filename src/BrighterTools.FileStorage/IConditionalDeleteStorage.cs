namespace BrighterTools.FileStorage;

/// <summary>Optional bounded deletion of an observed version. Permanent protocol seals cannot be deleted.</summary>
public interface IConditionalDeleteStorage
{
    /// <summary>Deletes only the observed object version. Returns false if absent; changed versions fail.</summary>
    Task<bool> DeleteObservedAsync(string path, string container, StorageObjectMetadata observed,
        TimeSpan timeout, CancellationToken cancellationToken = default);
}

