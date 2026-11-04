using System.Net;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using BrighterTools.FileStorage.AzureBlob;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BrighterTools.FileStorage.Tests;

public sealed class AzureBlobImmutableCopyTests
{
    private sealed class Transport : HttpMessageHandler
    {
        public int Calls;
        public HttpStatusCode Status = HttpStatusCode.Accepted;
        public bool Hang, LostResponse;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/destination/owner/file.pdf", request.RequestUri!.AbsolutePath);
            Assert.Equal("https://fixture.invalid/source/owner/file.pdf", request.Headers.GetValues("x-ms-copy-source").Single());
            Assert.Equal("\"source-version\"", request.Headers.GetValues("x-ms-source-if-match").Single());
            Assert.Equal("*", request.Headers.GetValues("If-None-Match").Single());
            Assert.Equal("true", request.Headers.GetValues("x-ms-requires-sync").Single());
            if (request.Content != null) Assert.Empty(await request.Content.ReadAsByteArrayAsync(ct));
            if (Hang) await Task.Delay(Timeout.Infinite, ct);
            if (LostResponse) throw new IOException("Synthetic lost response after copy");
            var result = new HttpResponseMessage(Status) { Content = new ByteArrayContent([]) };
            result.Headers.TryAddWithoutValidation("ETag", "\"destination-version\"");
            result.Headers.TryAddWithoutValidation("x-ms-copy-id", Guid.NewGuid().ToString());
            result.Headers.TryAddWithoutValidation("x-ms-copy-status", "success");
            result.Content.Headers.TryAddWithoutValidation("Last-Modified", "Wed, 04 Nov 2026 12:00:00 GMT");
            return result;
        }
    }
    private static AzureBlobFileStorageService Service(Transport transport)
    {
        var options = new BlobClientOptions { Transport = new HttpClientTransport(new HttpClient(transport)) };
        options.Retry.MaxRetries = 0;
        return new(Options.Create(new AzureBlobStorageOptions { ConnectionString = "UseDevelopmentStorage=true", ContainerName = "unused" }),
            NullLogger<AzureBlobFileStorageService>.Instance, new BlobServiceClient(new Uri("https://fixture.invalid/"), options));
    }
    private static Task<bool> Copy(AzureBlobFileStorageService service, CancellationToken ct = default, TimeSpan? timeout = null,
        long length = 100, string etag = "\"source-version\"") => service.CopyImmutableAsync("owner/file.pdf", "source",
            new(length, etag, DateTimeOffset.UtcNow, "application/pdf"), "owner/file.pdf", "destination", timeout ?? TimeSpan.FromSeconds(5), ct);

    [Fact]
    public async Task Copies_exact_version_without_uploading_bytes_or_overwriting_destination()
    {
        var transport = new Transport();
        Assert.True(await Copy(Service(transport))); Assert.Equal(1, transport.Calls);
        transport.Status = HttpStatusCode.PreconditionFailed;
        Assert.False(await Copy(Service(transport))); Assert.Equal(2, transport.Calls);
    }
    [Fact]
    public async Task Missing_source_and_uncertain_response_do_not_trigger_cleanup_or_overwrite()
    {
        var transport = new Transport { Status = HttpStatusCode.NotFound };
        await Assert.ThrowsAsync<FileNotFoundException>(() => Copy(Service(transport)));
        transport.Status = HttpStatusCode.Accepted; transport.LostResponse = true;
        await Assert.ThrowsAnyAsync<Exception>(() => Copy(Service(transport)));
        Assert.Equal(2, transport.Calls);
    }
    [Fact]
    public async Task Cancellation_and_timeout_bound_the_copy_request()
    {
        var transport = new Transport { Hang = true }; var service = Service(transport);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Copy(service, cancelled.Token)); Assert.Equal(0, transport.Calls);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Copy(service, timeout: TimeSpan.FromMilliseconds(50)));
        Assert.Equal(1, transport.Calls);
    }
    [Theory]
    [InlineData(0, "\"source-version\"")]
    [InlineData(268435457, "\"source-version\"")]
    [InlineData(10, "*")]
    public async Task Invalid_evidence_is_rejected_before_io(long length, string etag)
    {
        var transport = new Transport();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Copy(Service(transport), length: length, etag: etag));
        Assert.Equal(0, transport.Calls);
    }
}
