namespace BrighterTools.FileStorage.AzureBlob;

public partial class AzureBlobFileStorageService : IFileStorageIdentity
{
    /// <inheritdoc />
    public Uri ServiceEndpoint => new UriBuilder(_blobServiceClient.Uri)
    { UserName = "", Password = "", Query = "", Fragment = "" }.Uri;
}
