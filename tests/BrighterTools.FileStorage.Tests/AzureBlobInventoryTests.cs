using BrighterTools.FileStorage.AzureBlob;
using Azure;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BrighterTools.FileStorage.Tests;

public class AzureBlobInventoryTests
{
    [Theory]
    [InlineData(200, true)]
    [InlineData(404, false)]
    public async Task ListsWithoutContainerManagementPermissionAndHandlesMissingContainer(int status, bool expected)
    {
        var handler = new ListTransport(status);
        var service = Service(handler);
        var items = new List<StoredFileInfo>();
        await foreach (var item in service.EnumerateFiles("tenant", "files")) items.Add(item);
        Assert.Equal(expected ? 1 : 0, items.Count);
        Assert.Equal(1, handler.Calls);
        if (expected) Assert.Equal("tenant/object", items[0].StoredName);
    }

    [Fact]
    public async Task AuthorizationFailureIsNotTreatedAsEmptyInventory()
    {
        await using var items = Service(new ListTransport(403)).EnumerateFiles("tenant", "files").GetAsyncEnumerator();
        var error = await Assert.ThrowsAsync<RequestFailedException>(async () => await items.MoveNextAsync());
        Assert.Equal(403, error.Status);
    }

    private static AzureBlobFileStorageService Service(ListTransport handler)
    {
        var options = new BlobClientOptions { Transport = new HttpClientTransport(new HttpClient(handler)) };
        options.Retry.MaxRetries = 0;
        return new(Options.Create(new AzureBlobStorageOptions { ContainerName = "files", AccountName = "fixture", AccountKey = "fixture" }),
            NullLogger<AzureBlobFileStorageService>.Instance, new BlobServiceClient(new Uri("https://fixture.invalid"), options));
    }
    private sealed class ListTransport(int status) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains("comp=list", request.RequestUri!.Query);
            Assert.Contains("prefix=tenant", request.RequestUri.Query);
            var code = status == 404 ? "ContainerNotFound" : "AuthorizationFailure";
            var content = status == 200
                ? "<EnumerationResults ServiceEndpoint=\"https://fixture.invalid/\" ContainerName=\"files\"><Prefix>tenant/</Prefix><Blobs><Blob><Name>tenant/object</Name><Properties><Content-Length>3</Content-Length><BlobType>BlockBlob</BlobType></Properties><Metadata /></Blob></Blobs><NextMarker /></EnumerationResults>"
                : $"<Error><Code>{code}</Code><Message>fixture</Message></Error>";
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(content, System.Text.Encoding.UTF8, "application/xml") };
            if (status != 200) response.Headers.Add("x-ms-error-code",code);
            return Task.FromResult(response);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/")]
    [InlineData("../outside")]
    public async Task InvalidRootsAreRejectedBeforeAnyStorageRequest(string root)
    {
        var service = new AzureBlobFileStorageService(Options.Create(new AzureBlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true", ContainerName = "files",
            AccountName = "devstoreaccount1", AccountKey = "fixture"
        }), NullLogger<AzureBlobFileStorageService>.Instance);
        await using var files = service.EnumerateFiles(root, "files").GetAsyncEnumerator();
        await Assert.ThrowsAsync<ArgumentException>(async () => await files.MoveNextAsync());
    }
}