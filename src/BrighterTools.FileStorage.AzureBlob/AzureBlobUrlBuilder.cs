namespace BrighterTools.FileStorage.AzureBlob;

/// <summary>
/// Represents Azure Blob URL Builder.
/// </summary>
public static class AzureBlobUrlBuilder
{
    /// <summary>
    /// Builds Public URL.
    /// </summary>
    public static string BuildPublicUrl(string publicBaseUrl, string containerName, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            throw new ArgumentException("Public base URL cannot be null or empty.", nameof(publicBaseUrl));
        }

        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new ArgumentException("Container name cannot be null or empty.", nameof(containerName));
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Relative path cannot be null or empty.", nameof(relativePath));
        }

        return $"{publicBaseUrl.TrimEnd('/')}/{containerName}/{relativePath.TrimStart('/')}";
    }
}

