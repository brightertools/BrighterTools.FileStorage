using System.Net;
using Azure;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using BrighterTools.FileStorage.AzureBlob;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BrighterTools.FileStorage.Tests;

public class AzureBlobPrivacyTests
{
    private sealed class Transport(HttpStatusCode status, string? access) : HttpMessageHandler
    {
        public int Calls;
        public bool Hang;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/private-files", request.RequestUri!.AbsolutePath);
            Assert.Contains("restype=container", request.RequestUri.Query);
            if (Hang) await Task.Delay(Timeout.Infinite, token);
            var response = new HttpResponseMessage(status) { Content = new ByteArrayContent([]) };
            response.Headers.TryAddWithoutValidation("ETag", "\"fixture\"");
            response.Content.Headers.TryAddWithoutValidation("Last-Modified", "Wed, 04 Nov 2026 12:00:00 GMT");
            if (access is not null) response.Headers.TryAddWithoutValidation("x-ms-blob-public-access", access);
            return response;
        }
    }

    private static AzureBlobFileStorageService Service(Transport transport)
    {
        var options = new BlobClientOptions { Transport = new HttpClientTransport(new HttpClient(transport)) };
        options.Retry.MaxRetries = 0;
        return new(Options.Create(new AzureBlobStorageOptions { ConnectionString = "UseDevelopmentStorage=true", ContainerName = "wrong-default" }),
            NullLogger<AzureBlobFileStorageService>.Instance, new BlobServiceClient(new Uri("https://fixture.invalid"), options));
    }

    [Fact]
    public async Task Private_container_requires_only_a_read_and_no_duplicate_account_credentials()
    {
        var transport = new Transport(HttpStatusCode.OK, null);
        await Service(transport).EnsurePrivateAsync("private-files", TimeSpan.FromSeconds(5));
        Assert.Equal(1, transport.Calls);
    }

    [Theory]
    [InlineData("blob")]
    [InlineData("container")]
    public async Task Public_containers_are_rejected(string access)
    {
        var transport = new Transport(HttpStatusCode.OK, access);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(transport).EnsurePrivateAsync("private-files", TimeSpan.FromSeconds(5)));
        Assert.Equal(1, transport.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Missing_or_inaccessible_containers_fail_without_creation_or_acl_changes(HttpStatusCode status)
    {
        var transport = new Transport(status, null);
        await Assert.ThrowsAsync<RequestFailedException>(() => Service(transport).EnsurePrivateAsync("private-files", TimeSpan.FromSeconds(5)));
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task Cancellation_and_timeout_are_observed()
    {
        var transport = new Transport(HttpStatusCode.OK, null) { Hang = true };
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(transport).EnsurePrivateAsync("private-files", TimeSpan.FromSeconds(5), cancelled.Token));
        Assert.Equal(0, transport.Calls);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(transport).EnsurePrivateAsync("private-files", TimeSpan.FromMilliseconds(50)));
        Assert.Equal(1, transport.Calls);
    }
}