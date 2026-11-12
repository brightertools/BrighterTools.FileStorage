namespace BrighterTools.FileStorage;

/// <summary>Optional verification of existing, non-public storage containers.</summary>
public interface IPrivateFileStorage
{
    /// <summary>
    /// Verifies that a container exists and denies anonymous access. Throws if it is
    /// missing, public or inaccessible. Does not create containers or change access.
    /// This observation does not authorize the caller or certify account/network policy.
    /// </summary>
    Task EnsurePrivateAsync(string container, TimeSpan timeout, CancellationToken cancellationToken = default);
}