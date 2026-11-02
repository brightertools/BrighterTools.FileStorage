namespace BrighterTools.FileStorage;

/// <summary>Optional credential-free identity of the actual configured storage transport.</summary>
public interface IFileStorageIdentity
{
    /// <summary>Service root, including an emulator account path if required, without credentials or query parameters.</summary>
    Uri ServiceEndpoint { get; }
}
