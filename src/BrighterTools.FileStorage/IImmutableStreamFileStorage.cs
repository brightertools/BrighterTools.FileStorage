namespace BrighterTools.FileStorage;

/// <summary>Optional bounded-memory, create-only uploads. Hosts persist intent before calling.</summary>
public interface IImmutableStreamFileStorage
{
    /// <summary>
    /// Writes exactly length bytes, never replacing an existing object or retirement seal.
    /// False means the final create condition failed; it is not proof that existing bytes match.
    /// Throws on uncertain outcomes. Never refund capacity or delete safety markers on an exception.
    /// Caller retains ownership of content. Timeout is per block/read/commit, not the whole upload.
    /// Uncommitted provider blocks may remain temporarily after cancellation or failed commitment.
    /// </summary>
    Task<bool> CreateImmutableStreamAsync(string path, string container, Stream content, long length,
        string contentType, TimeSpan operationTimeout, CancellationToken cancellationToken = default);
}
