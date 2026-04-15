namespace BrighterTools.FileStorage.AzureBlob;

/// <summary>
/// Represents configuration options for Azure Blob Storage.
/// </summary>
public class AzureBlobStorageOptions
{
    /// <summary>
    /// Gets or sets the Connection String.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Container Name.
    /// </summary>
    public string ContainerName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Account Name.
    /// </summary>
    public string AccountName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Account Key.
    /// </summary>
    public string AccountKey { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Max Blob Path Length.
    /// </summary>
    public int MaxBlobPathLength { get; set; } = 1000;
    /// <summary>
    /// Gets or sets the Public Base URL.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cdn Endpoint Hostname.
    /// </summary>
    public string CdnEndpointHostname
    {
        get => PublicBaseUrl;
        set => PublicBaseUrl = value;
    }
}

