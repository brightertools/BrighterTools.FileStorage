namespace BrighterTools.FileStorage;

/// <summary>
/// Represents Stored File Info.
/// </summary>
public class StoredFileInfo
{
    /// <summary>
    /// Gets or sets the Stored Name.
    /// </summary>
    public string StoredName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Last Modified.
    /// </summary>
    public DateTimeOffset? LastModified { get; set; }
    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the file Size.
    /// </summary>
    public long FileSize => Metadata.TryGetValue("file-size", out var size) && long.TryParse(size, out var fileSize) ? fileSize : 0;
}

