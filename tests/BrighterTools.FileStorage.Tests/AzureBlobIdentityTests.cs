using Azure.Storage.Blobs;
using BrighterTools.FileStorage.AzureBlob;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BrighterTools.FileStorage.Tests;

public class AzureBlobIdentityTests
{
    [Fact]
    public void IdentityUsesActualTransportNotConfiguredPublicUrlAndNeverReturnsSas()
    {
        var client = new BlobServiceClient(new Uri("https://fixture.blob.core.windows.net/?sv=2026-01-01&sig=fixture-secret"));
        var options = Options.Create(new AzureBlobStorageOptions { ContainerName = "fixture", AccountName = "fixture", AccountKey = "fixture-key", PublicBaseUrl = "https://cdn.example.invalid" });
        IFileStorageIdentity storage = new AzureBlobFileStorageService(options, NullLogger<AzureBlobFileStorageService>.Instance, client);
        Assert.Equal("https://fixture.blob.core.windows.net/", storage.ServiceEndpoint.AbsoluteUri);
        Assert.Equal("", storage.ServiceEndpoint.Query);
    }
}
