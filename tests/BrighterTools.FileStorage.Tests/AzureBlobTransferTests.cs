using System.Net;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using BrighterTools.FileStorage.AzureBlob;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BrighterTools.FileStorage.Tests;

public class AzureBlobTransferTests
{
    private sealed class Transport : HttpMessageHandler
    {
        public byte[]? Bytes = [1, 2, 3];
        public bool Sealed;
        public int Version = 1;
        public long? DeclaredLength;
        public byte[]? ResponseBytes;
        public bool ChangeBeforeGet;
        public bool Hang;
        public int Gets;
        public int Calls;
        public Func<Task>? BeforePut;
        public string? LastCondition;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            if (Hang) await Task.Delay(Timeout.Infinite, ct);
            if (request.Method == HttpMethod.Put)
            {
                var payload = await request.Content!.ReadAsByteArrayAsync(ct);
                var condition = request.Headers.TryGetValues("If-None-Match", out var values) ? values.Single() : null;
                LastCondition = condition;
                if (payload.Length > 0 && BeforePut is not null) await BeforePut();
                if (condition == "*" && Bytes is not null) return Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet");
                Bytes = payload;
                Sealed = request.Headers.TryGetValues("x-ms-meta-btstorage", out var p) && p.Single() == "sealed-v1";
                Version++;
                return Reply(HttpStatusCode.Created, []);
            }
            if (Bytes is null) return Error(HttpStatusCode.NotFound, "BlobNotFound");
            if (request.Method == HttpMethod.Head) return Reply(HttpStatusCode.OK, [] , DeclaredLength ?? Bytes.Length);
            Gets++;
            Assert.Contains("Range", string.Join(',', request.Headers.Select(x => x.Key)), StringComparison.OrdinalIgnoreCase);
            Assert.True(request.Headers.Contains("If-Match"));
            if (ChangeBeforeGet) return Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet");
            var response = Reply(HttpStatusCode.PartialContent, ResponseBytes ?? Bytes, DeclaredLength ?? Bytes.Length);
            response.Content.Headers.TryAddWithoutValidation("Content-Range", $"bytes 0-{Bytes.Length - 1}/{Bytes.Length}");
            return response;
        }
        private HttpResponseMessage Reply(HttpStatusCode code, byte[] bytes, long? length = null)
        {
            var response = new HttpResponseMessage(code) { Content = new ByteArrayContent(bytes) };
            response.Content.Headers.ContentLength = length ?? bytes.Length;
            response.Headers.TryAddWithoutValidation("ETag", $"\"v{Version}\"");
            response.Content.Headers.TryAddWithoutValidation("Last-Modified", "Wed, 21 Oct 2026 12:00:00 GMT");
            response.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
            if (Sealed) response.Headers.TryAddWithoutValidation("x-ms-meta-btstorage", "sealed-v1");
            return response;
        }
        private static HttpResponseMessage Error(HttpStatusCode status, string code)
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent($"<Error><Code>{code}</Code><Message>fixture</Message></Error>") };
            response.Headers.TryAddWithoutValidation("x-ms-error-code", code); return response;
        }
    }

    private static AzureBlobFileStorageService Service(Transport transport)
    {
        var options = new BlobClientOptions { Transport = new HttpClientTransport(new HttpClient(transport)) };
        options.Retry.MaxRetries = 0;
        return new(Options.Create(new AzureBlobStorageOptions { ConnectionString = "UseDevelopmentStorage=true", ContainerName = "files", AccountName = "fixture", AccountKey = "fixture" }),
            NullLogger<AzureBlobFileStorageService>.Instance, new BlobServiceClient(new Uri("https://fixture.invalid/"), options));
    }
    private static readonly TimeSpan TimeoutBudget = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task BoundedReadPinsVersionAndReturnsExactBytes()
    {
        var t = new Transport(); var result = await Service(t).DownloadBoundedAsync("tenant/file", "files", 3, TimeoutBudget);
        Assert.Equal(t.Bytes, result); Assert.Equal(1, t.Gets);
    }
    [Fact]
    public async Task OversizedMetadataRejectsBeforeBodyRequest()
    {
        var t = new Transport { DeclaredLength = 1000000000 };
        await Assert.ThrowsAsync<InvalidDataException>(() => Service(t).DownloadBoundedAsync("tenant/file", "files", 10, TimeoutBudget));
        Assert.Equal(0, t.Gets);
    }
    [Fact]
    public async Task ChangedVersionCannotBeDownloaded()
    {
        var t = new Transport { ChangeBeforeGet = true };
        await Assert.ThrowsAsync<Azure.RequestFailedException>(() => Service(t).DownloadBoundedAsync("tenant/file", "files", 10, TimeoutBudget));
    }
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task TruncatedOrOverlongBodyIsRejected(int length)
    {
        var t = new Transport { ResponseBytes = new byte[length] };
        await Assert.ThrowsAsync<InvalidDataException>(() => Service(t).DownloadBoundedAsync("tenant/file", "files", 10, TimeoutBudget));
    }
    [Fact]
    public async Task MissingAndEmptyObjectsHaveExplicitResults()
    {
        var t = new Transport { Bytes = null }; var service = Service(t);
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.DownloadBoundedAsync("tenant/file", "files", 10, TimeoutBudget));
        t.Bytes = [];
        Assert.Empty(await service.DownloadBoundedAsync("tenant/file", "files", 10, TimeoutBudget));
        Assert.Equal(0, t.Gets);
    }
    [Fact]
    public async Task CancellationAndTimeoutReachTransport()
    {
        var t = new Transport { Hang = true }; var service = Service(t);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DownloadBoundedAsync("tenant/file", "files", 10, TimeoutBudget, cancelled.Token));
        Assert.Equal(0, t.Calls);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DownloadBoundedAsync("tenant/file", "files", 10, TimeSpan.FromMilliseconds(30)));
    }
    [Theory]
    [InlineData("")]
    [InlineData("/absolute")]
    [InlineData("../outside")]
    [InlineData("tenant/%2e/file")]
    [InlineData("https://outside/file")]
    [InlineData("tenant//file")]
    public async Task UnsafePathsRejectBeforeTransport(string path)
    {
        var t = new Transport(); var s = Service(t);
        await Assert.ThrowsAsync<ArgumentException>(() => s.DownloadBoundedAsync(path, "files", 10, TimeoutBudget));
        await Assert.ThrowsAsync<ArgumentException>(() => s.SealAsync(path, "files", TimeoutBudget));
        Assert.Equal(0, t.Calls);
    }
    [Fact]
    public async Task CreateIsConditionalAndNeverOverwrites()
    {
        var t = new Transport { Bytes = null }; var s = Service(t);
        Assert.True(await s.CreateImmutableAsync("tenant/file", "files", [4, 5], "image/png", TimeoutBudget));
        Assert.Equal("*", t.LastCondition);
        Assert.False(await s.CreateImmutableAsync("tenant/file", "files", [9], "image/png", TimeoutBudget));
        Assert.Equal(new byte[] { 4, 5 }, t.Bytes);
    }
    [Fact]
    public async Task SealBeforeOrAfterWritePermanentlyRejectsLateCreate()
    {
        var t = new Transport { Bytes = null }; var s = Service(t);
        await s.SealAsync("tenant/file", "files", TimeoutBudget);
        Assert.True(await s.IsSealedAsync("tenant/file", "files", TimeoutBudget));
        Assert.False(await s.CreateImmutableAsync("tenant/file", "files", [9], "image/png", TimeoutBudget));
        t.Bytes = [1]; t.Sealed = false;
        await s.SealAsync("tenant/file", "files", TimeoutBudget);
        Assert.Empty(t.Bytes!); Assert.True(await s.IsSealedAsync("tenant/file", "files", TimeoutBudget));
    }
    [Fact]
    public async Task InFlightLateWriteCannotReplaceSeal()
    {
        var t = new Transport { Bytes = null }; var s = Service(t);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        t.BeforePut = async () => { entered.SetResult(); await release.Task; };
        var late = s.CreateImmutableAsync("tenant/file", "files", [9], "image/png", TimeoutBudget);
        await entered.Task.WaitAsync(TimeoutBudget);
        await s.SealAsync("tenant/file", "files", TimeoutBudget);
        release.SetResult();
        Assert.False(await late); Assert.Empty(t.Bytes!);
    }
    [Fact]
    public async Task MetadataAloneDoesNotMakePayloadASeal()
    {
        var t = new Transport { Sealed = true, Bytes = [1] };
        Assert.False(await Service(t).IsSealedAsync("tenant/file", "files", TimeoutBudget));
    }
}
