using System.Net;
using Azure;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using BrighterTools.FileStorage.AzureBlob;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BrighterTools.FileStorage.Tests;

public class AzureBlobStreamingTests
{
    private sealed class Body(byte[] bytes) : MemoryStream(bytes)
    {
        public int Reads;
        public bool Released;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) { Reads++; return base.ReadAsync(buffer, ct); }
        protected override void Dispose(bool disposing) { Released = true; base.Dispose(disposing); }
    }
    private sealed class Transport : HttpMessageHandler
    {
        public long Length = 10;
        public long Offset = 2;
        public long Count = 3;
        public Body Body = new([2, 3, 4]);
        public bool Sealed;
        public bool Changed;
        public bool Missing;
        public bool WrongRange;
        public bool Hang;
        public int Calls;
        public string? Range;
        public string? Match;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            if (Hang) await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            Assert.True(request.Method == HttpMethod.Head || request.Method == HttpMethod.Get);
            var head = request.Method == HttpMethod.Head;
            if (!head)
            {
                Range = request.Headers.TryGetValues("x-ms-range", out var ranges) ? ranges.Single() : request.Headers.Range?.ToString();
                Match = request.Headers.IfMatch.Single().ToString();
            }
            if (Missing || Changed && !head)
            {
                var code = Missing ? "BlobNotFound" : "ConditionNotMet";
                var error = new HttpResponseMessage(Missing ? HttpStatusCode.NotFound : HttpStatusCode.PreconditionFailed)
                { Content = new StringContent($"<Error><Code>{code}</Code></Error>") };
                error.Headers.TryAddWithoutValidation("x-ms-error-code", code);
                return error;
            }
            var response = new HttpResponseMessage(head ? HttpStatusCode.OK : HttpStatusCode.PartialContent)
            { Content = head ? new ByteArrayContent([]) : new StreamContent(Body) };
            response.Headers.ETag = new("\"v1\"");
            response.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
            response.Content.Headers.ContentLength = head ? Length : Count;
            response.Content.Headers.TryAddWithoutValidation("Last-Modified", "Thu, 22 Oct 2026 12:00:00 GMT");
            response.Content.Headers.TryAddWithoutValidation("Content-Type", "video/mp4");
            if (!head) response.Content.Headers.TryAddWithoutValidation("Content-Range", $"bytes {(WrongRange ? 0 : Offset)}-{Offset + Count - 1}/{Length}");
            if (Sealed) response.Headers.TryAddWithoutValidation("x-ms-meta-btstorage", "sealed-v1");
            return response;
        }
    }
    private static AzureBlobFileStorageService Service(Transport transport)
    {
        var options = new BlobClientOptions { Transport = new HttpClientTransport(new HttpClient(transport)) };
        options.Retry.MaxRetries = 0;
        return new(Options.Create(new AzureBlobStorageOptions { ConnectionString = "UseDevelopmentStorage=true", ContainerName = "files", AccountName = "fixture", AccountKey = "fixture" }),
            NullLogger<AzureBlobFileStorageService>.Instance, new BlobServiceClient(new Uri("https://fixture.invalid"), options));
    }
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);
    [Fact]
    public async Task ReadsExactRangeWithoutBufferingAndPinsVersion()
    {
        var transport = new Transport { Length = 10_000_000_000 };
        var service = Service(transport);
        var metadata = await service.ReadMetadataAsync("tenant/video", "private-files", Budget);
        Assert.Equal(10_000_000_000, metadata.Length);
        Assert.Equal("video/mp4", metadata.ContentType);
        await using (var stream = await service.OpenReadRangeAsync("tenant/video", "private-files", metadata, 2, 3, Budget))
        {
            Assert.Equal(0, transport.Body.Reads);
            Assert.False(stream.CanSeek);
            using var result = new MemoryStream();
            await stream.CopyToAsync(result);
            Assert.Equal(new byte[] { 2, 3, 4 }, result.ToArray());
        }
        Assert.Equal("bytes=2-4", transport.Range);
        Assert.Equal("\"v1\"", transport.Match);
        Assert.True(transport.Body.Released);
    }
    [Fact]
    public async Task RetiredOrMissingObjectsCannotBeRead()
    {
        foreach (var transport in new[] { new Transport { Sealed = true }, new Transport { Missing = true } })
            await Assert.ThrowsAsync<FileNotFoundException>(() => Service(transport).ReadMetadataAsync("tenant/file", "files", Budget));
    }
    [Fact]
    public async Task ChangedObjectFailsRatherThanMixingVersions()
    {
        var transport = new Transport { Changed = true }; var service = Service(transport);
        var metadata = await service.ReadMetadataAsync("tenant/file", "files", Budget);
        await Assert.ThrowsAsync<RequestFailedException>(() => service.OpenReadRangeAsync("tenant/file", "files", metadata, 2, 3, Budget));
    }
    [Fact]
    public async Task WrongRangeDisposesResponse()
    {
        var transport = new Transport { WrongRange = true }; var service = Service(transport);
        var metadata = await service.ReadMetadataAsync("tenant/file", "files", Budget);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.OpenReadRangeAsync("tenant/file", "files", metadata, 2, 3, Budget));
        Assert.True(transport.Body.Released);
    }
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task IncorrectBodyLengthFails(int length)
    {
        var transport = new Transport { Body = new(new byte[length]) }; var service = Service(transport);
        var metadata = await service.ReadMetadataAsync("tenant/file", "files", Budget);
        await using var body = await service.OpenReadRangeAsync("tenant/file", "files", metadata, 2, 3, Budget);
        await Assert.ThrowsAsync<InvalidDataException>(() => body.CopyToAsync(Stream.Null));
    }
    [Theory]
    [InlineData(-1, 2)]
    [InlineData(2, -1)]
    [InlineData(9, 2)]
    [InlineData(long.MaxValue, long.MaxValue)]
    public async Task InvalidRangeDoesNotAccessStorage(long offset, long length)
    {
        var transport = new Transport();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Service(transport).OpenReadRangeAsync("tenant/file", "files",
            new(10, "\"v1\"", DateTimeOffset.UtcNow, "image/png"), offset, length, Budget));
        Assert.Equal(0, transport.Calls);
    }
    [Fact]
    public async Task EmptyObjectDoesNotIssueBodyGet()
    {
        var transport = new Transport { Length = 0 }; var service = Service(transport);
        var metadata = await service.ReadMetadataAsync("tenant/empty", "files", Budget);
        await using var stream = await service.OpenReadRangeAsync("tenant/empty", "files", metadata, 0, 0, Budget);
        Assert.Equal(-1, stream.ReadByte()); Assert.Equal(1, transport.Calls);
    }
    [Fact]
    public async Task RequestCancellationAppliesAfterOpening()
    {
        var transport = new Transport(); var service = Service(transport); using var cts = new CancellationTokenSource();
        var metadata = await service.ReadMetadataAsync("tenant/file", "files", Budget);
        await using var body = await service.OpenReadRangeAsync("tenant/file", "files", metadata, 2, 3, Budget, cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => body.ReadAsync(new byte[1]).AsTask());
    }
    [Fact]
    public async Task HeaderTimeoutCancelsTransport()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(new Transport { Hang = true })
            .ReadMetadataAsync("tenant/file", "files", TimeSpan.FromMilliseconds(20)));
    }
    [Theory]
    [InlineData("../outside")]
    [InlineData("https://outside/file")]
    [InlineData("tenant/%2e/file")]
    public async Task UnsafePathRejectsBeforeTransport(string path)
    {
        var transport = new Transport();
        await Assert.ThrowsAsync<ArgumentException>(() => Service(transport).ReadMetadataAsync(path, "files", Budget));
        Assert.Equal(0, transport.Calls);
    }
}
