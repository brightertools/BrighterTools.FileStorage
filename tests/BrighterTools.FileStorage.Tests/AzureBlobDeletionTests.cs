using System.Net;
using Azure;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using BrighterTools.FileStorage.AzureBlob;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BrighterTools.FileStorage.Tests;

public class AzureBlobDeletionTests
{
    private sealed class Transport : HttpMessageHandler
    {
        public bool Missing, Changed, Sealed, Hang, Race;
        public int Deletes;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (Hang) await Task.Delay(Timeout.Infinite, token);
            if (request.Method == HttpMethod.Delete)
            {
                Deletes++;
                Assert.Equal("\"v1\"", request.Headers.IfMatch.Single().ToString());
                return new(Race ? HttpStatusCode.PreconditionFailed : HttpStatusCode.Accepted) { Content = new ByteArrayContent([]) };
            }
            Assert.Equal(HttpMethod.Head, request.Method);
            var response = new HttpResponseMessage(Missing ? HttpStatusCode.NotFound : HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
            response.Headers.TryAddWithoutValidation("ETag", Changed ? "\"v2\"" : "\"v1\"");
            response.Headers.TryAddWithoutValidation("Last-Modified", "Thu, 05 Nov 2026 12:00:00 GMT");
            response.Content.Headers.ContentLength = 10;
            if (Sealed) response.Headers.TryAddWithoutValidation("x-ms-meta-btstorage", "sealed-v1");
            return response;
        }
    }
    private static AzureBlobFileStorageService Service(Transport transport)
    {
        var options = new BlobClientOptions { Transport = new HttpClientTransport(new HttpClient(transport)) };
        options.Retry.MaxRetries = 0;
        return new(Options.Create(new AzureBlobStorageOptions { ConnectionString = "UseDevelopmentStorage=true", ContainerName = "files" }),
            NullLogger<AzureBlobFileStorageService>.Instance, new BlobServiceClient(new Uri("https://fixture.invalid"), options));
    }
    private static StorageObjectMetadata Observed => new(10, "\"v1\"", DateTimeOffset.UtcNow, "application/pdf");
    [Fact] public async Task Deletes_the_observed_version_and_missing_is_idempotent()
    {
        var t = new Transport(); Assert.True(await Service(t).DeleteObservedAsync("a/file.pdf", "files", Observed, TimeSpan.FromSeconds(2)));
        Assert.Equal(1, t.Deletes); t.Missing = true;
        Assert.False(await Service(t).DeleteObservedAsync("a/file.pdf", "files", Observed, TimeSpan.FromSeconds(2)));
        Assert.Equal(1, t.Deletes);
    }
    [Fact] public async Task Changed_versions_and_seals_are_not_deleted()
    {
        var t = new Transport { Changed = true };
        await Assert.ThrowsAsync<IOException>(() => Service(t).DeleteObservedAsync("a/file.pdf", "files", Observed, TimeSpan.FromSeconds(2)));
        t.Changed = false; t.Sealed = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(t).DeleteObservedAsync("a/file.pdf", "files", Observed, TimeSpan.FromSeconds(2)));
        Assert.Equal(0, t.Deletes);
        t.Sealed = false; t.Race = true;
        await Assert.ThrowsAsync<RequestFailedException>(() => Service(t).DeleteObservedAsync("a/file.pdf", "files", Observed, TimeSpan.FromSeconds(2)));
    }
    [Fact] public async Task Cancellation_deadline_and_exact_version_are_required()
    {
        var t = new Transport { Hang = true }; var s = Service(t);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => s.DeleteObservedAsync("a/file.pdf", "files", Observed, TimeSpan.FromMilliseconds(30)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => s.DeleteObservedAsync("a/file.pdf", "files", Observed, TimeSpan.FromSeconds(2), new(true)));
        await Assert.ThrowsAsync<ArgumentException>(() => s.DeleteObservedAsync("a/file.pdf", "files", Observed with { ETag = "*" }, TimeSpan.FromSeconds(2)));
    }
}
