using System.Net;
using System.Xml.Linq;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using BrighterTools.FileStorage.AzureBlob;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BrighterTools.FileStorage.Tests;

public class AzureBlobImmutableStreamingTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);
    private sealed class Transport : HttpMessageHandler
    {
        public byte[]? Bytes;
        public bool Sealed;
        public readonly Dictionary<string, byte[]> Blocks = [];
        public readonly List<string> AttemptBlocks = [];
        public readonly List<string> Containers = [];
        public int Commits, Calls, MaxBlock;
        public string? Condition;
        public Func<Task>? BeforeCommit;
        public bool HangStage, LoseCommitResponse;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Assert.Equal(HttpMethod.Put, request.Method);
            var uri = request.RequestUri!; Containers.Add(uri.AbsolutePath);
            var query = uri.Query.TrimStart('?').Split('&').Where(x => x.Length > 0)
                .Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => Uri.UnescapeDataString(x[1]));
            var data = await request.Content!.ReadAsByteArrayAsync(ct);
            if (query.GetValueOrDefault("comp") == "block")
            {
                if (HangStage) await Task.Delay(Timeout.Infinite, ct);
                MaxBlock = Math.Max(MaxBlock, data.Length);
                Blocks[query["blockid"]] = data; AttemptBlocks.Add(query["blockid"]);
            }
            else if (query.GetValueOrDefault("comp") == "blocklist")
            {
                Commits++;
                Condition = request.Headers.GetValues("If-None-Match").Single();
                Assert.Equal("*", Condition);
                if (BeforeCommit is not null) await BeforeCommit();
                if (Bytes is not null) return Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet");
                var ids = XDocument.Load(new MemoryStream(data)).Root!.Elements().Select(x => x.Value);
                Bytes = ids.SelectMany(id => Blocks[id]).ToArray();
                if (LoseCommitResponse) throw new IOException("Synthetic response loss");
            }
            else
            {
                Assert.Empty(data); Bytes = []; Sealed = true;
            }
            var response = new HttpResponseMessage(HttpStatusCode.Created) { Content = new ByteArrayContent([]) };
            response.Headers.TryAddWithoutValidation("ETag", "\"fixture\"");
            response.Content.Headers.TryAddWithoutValidation("Last-Modified", "Wed, 21 Oct 2026 12:00:00 GMT");
            return response;
        }
        private static HttpResponseMessage Error(HttpStatusCode status, string code)
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent($"<Error><Code>{code}</Code></Error>") };
            response.Headers.TryAddWithoutValidation("x-ms-error-code", code); return response;
        }
    }
    private static AzureBlobFileStorageService Service(Transport t)
    {
        var options = new BlobClientOptions { Transport = new HttpClientTransport(new HttpClient(t)) }; options.Retry.MaxRetries = 0;
        return new(Options.Create(new AzureBlobStorageOptions { ConnectionString = "UseDevelopmentStorage=true", ContainerName = "wrong-default", AccountName = "fixture", AccountKey = "fixture" }),
            NullLogger<AzureBlobFileStorageService>.Instance, new BlobServiceClient(new Uri("https://fixture.invalid/"), options));
    }
    private sealed class NonSeekStream(byte[] bytes) : MemoryStream(bytes)
    {
        public int MaxRead;
        public override bool CanSeek => false;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        { MaxRead = Math.Max(MaxRead, buffer.Length); return base.ReadAsync(buffer, ct); }
    }
    [Fact]
    public async Task UploadIsBoundedExactCreateOnlyAndLeavesCallerStreamOpen()
    {
        var bytes = new byte[9 * 1024 * 1024 + 23]; new Random(17).NextBytes(bytes);
        using var source = new NonSeekStream(bytes); var t = new Transport();
        Assert.True(await Service(t).CreateImmutableStreamAsync("tenant/file/original", "private-test", source, bytes.Length, "application/octet-stream", Budget));
        Assert.Equal(bytes, t.Bytes); Assert.Equal(3, t.Blocks.Count); Assert.True(t.MaxBlock <= 4 * 1024 * 1024);
        Assert.True(source.MaxRead <= 64 * 1024); Assert.True(source.CanRead);
        Assert.All(t.Containers, x => Assert.StartsWith("/private-test/", x)); Assert.Equal(1, t.Commits);
    }
    [Theory]
    [InlineData(2)] [InlineData(4)]
    public async Task TruncationAndExtraBytesNeverCommit(int declared)
    {
        var t = new Transport(); using var source = new NonSeekStream([1, 2, 3]);
        await Assert.ThrowsAsync<InvalidDataException>(() => Service(t).CreateImmutableStreamAsync("tenant/file", "files", source, declared, "image/png", Budget));
        Assert.Equal(0, t.Commits); Assert.Null(t.Bytes); Assert.True(source.CanRead);
    }
    [Fact]
    public async Task SeekableLengthMismatchIsRejectedBeforeIO()
    {
        var t = new Transport(); using var source = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<InvalidDataException>(() => Service(t).CreateImmutableStreamAsync("tenant/file", "files", source, 2, "image/png", Budget));
        Assert.Equal(0, t.Calls);
    }
    [Fact]
    public async Task ExistingObjectAndSealsAreNeverReplaced()
    {
        var t = new Transport { Bytes = [5] }; var service = Service(t);
        Assert.False(await service.CreateImmutableStreamAsync("tenant/file", "files", new MemoryStream([9]), 1, "image/png", Budget));
        Assert.Equal(new byte[] { 5 }, t.Bytes);
        await service.SealAsync("tenant/file", "files", Budget);
        Assert.False(await service.CreateImmutableStreamAsync("tenant/file", "files", new MemoryStream([8]), 1, "image/png", Budget));
        Assert.True(t.Sealed); Assert.Empty(t.Bytes!);
    }
    [Fact]
    public async Task DelayedCommitCannotReplaceSealAndRetriesUseDifferentBlockIds()
    {
        var t = new Transport(); var service = Service(t);
        t.BeforeCommit = () => service.SealAsync("tenant/file", "files", Budget);
        Assert.False(await service.CreateImmutableStreamAsync("tenant/file", "files", new MemoryStream([9]), 1, "image/png", Budget));
        t.BeforeCommit = null;
        Assert.False(await service.CreateImmutableStreamAsync("tenant/file", "files", new MemoryStream([8]), 1, "image/png", Budget));
        Assert.Equal(2, t.AttemptBlocks.Distinct().Count()); Assert.Empty(t.Bytes!);
        Assert.Single(t.AttemptBlocks.Select(x => x.Length).Distinct());
    }
    [Fact]
    public async Task LostCommitResponseIsUncertainAndDoesNotDeleteOrOverwrite()
    {
        var t = new Transport { LoseCommitResponse = true }; var service = Service(t);
        await Assert.ThrowsAnyAsync<Exception>(() => service.CreateImmutableStreamAsync("tenant/file", "files", new MemoryStream([9]), 1, "image/png", Budget));
        Assert.Equal(new byte[] { 9 }, t.Bytes); Assert.False(t.Sealed);
        t.LoseCommitResponse = false;
        Assert.False(await service.CreateImmutableStreamAsync("tenant/file", "files", new MemoryStream([8]), 1, "image/png", Budget));
        Assert.Equal(new byte[] { 9 }, t.Bytes);
    }
    [Fact]
    public async Task CancellationAndPerOperationTimeoutDoNotCommit()
    {
        var t = new Transport { HangStage = true }; var service = Service(t);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateImmutableStreamAsync("tenant/file", "files", new MemoryStream([1]), 1, "image/png", Budget, cts.Token));
        Assert.Equal(0, t.Calls);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateImmutableStreamAsync("tenant/file", "files", new MemoryStream([1]), 1, "image/png", TimeSpan.FromMilliseconds(50)));
        Assert.Equal(0, t.Commits);
    }
    [Theory]
    [InlineData("../file")] [InlineData("/file")] [InlineData("tenant/%2f/file")]
    public async Task InvalidPathsNeverReachStorage(string path)
    {
        var t = new Transport();
        await Assert.ThrowsAsync<ArgumentException>(() => Service(t).CreateImmutableStreamAsync(path, "files", new MemoryStream([1]), 1, "image/png", Budget));
        Assert.Equal(0, t.Calls);
    }
}

