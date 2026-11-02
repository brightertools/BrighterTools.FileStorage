namespace BrighterTools.FileStorage;

/// <summary>Optional bounded, cancellable reads. Existing storage contracts remain unchanged.</summary>
public interface IBoundedFileStorage
{
    /// <summary>Reads one consistent object, rejecting excessive size before buffering its body.</summary>
    Task<byte[]> DownloadBoundedAsync(string path, string container, long maxBytes, TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>Optional immutable object protocol. Sealed empty markers MUST NOT be deleted or expired.</summary>
public interface IImmutableFileStorage
{
    /// <summary>Creates content only if no object or seal exists. False means an existing object; never overwrites.</summary>
    Task<bool> CreateImmutableAsync(string path, string container, byte[] content, string contentType, TimeSpan timeout, CancellationToken cancellationToken = default);
    /// <summary>Replaces current contents with a permanent zero-byte seal, preventing late create-only writes.</summary>
    Task SealAsync(string path, string container, TimeSpan timeout, CancellationToken cancellationToken = default);
    /// <summary>Checks both protocol metadata and zero content length; missing objects are not sealed.</summary>
    Task<bool> IsSealedAsync(string path, string container, TimeSpan timeout, CancellationToken cancellationToken = default);
}
