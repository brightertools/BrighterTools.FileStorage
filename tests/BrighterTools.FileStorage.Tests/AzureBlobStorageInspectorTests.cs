using System.Net;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using BrighterTools.FileStorage.AzureBlob;

namespace BrighterTools.FileStorage.Tests;

public sealed class AzureBlobStorageInspectorTests
{
    private sealed class ReadOnlyTransport(string? access = null, string? retention = null) : HttpMessageHandler
    {
        public List<HttpMethod> Methods { get; } = [];
        public HttpStatusCode? Failure { get; set; }
        public bool Hang { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Methods.Add(request.Method);
            Assert.Contains(request.Method, new[] { HttpMethod.Head, HttpMethod.Get });
            Assert.DoesNotContain("comp=list", request.RequestUri!.Query);
            if (Hang) await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            if (Failure.HasValue) return new(Failure.Value) { Content = new StringContent("<Error><Code>Denied</Code><Message>private diagnostic</Message></Error>") };
            var container = request.RequestUri.Query.Contains("restype=container");
            Assert.Equal(HttpMethod.Get, request.Method);
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (container)
            {
                response.Headers.TryAddWithoutValidation("ETag", "\"fixture\"");
                response.Headers.TryAddWithoutValidation("Last-Modified", "Thu, 22 Oct 2026 00:00:00 GMT");
                response.Headers.TryAddWithoutValidation("x-ms-has-legal-hold", "false");
                response.Headers.TryAddWithoutValidation("x-ms-has-immutability-policy", "false");
                if (access is not null) response.Headers.TryAddWithoutValidation("x-ms-blob-public-access", access);
            }
            else response.Content = new StringContent("<StorageServiceProperties>" + retention + "</StorageServiceProperties>");
            return response;
        }
    }

    private static AzureBlobStorageInspector Inspector(ReadOnlyTransport transport)
    {
        var options = new BlobClientOptions { Transport = new HttpClientTransport(new HttpClient(transport)) };
        options.Retry.MaxRetries = 0;
        return new(new BlobServiceClient(new Uri("https://fixture.invalid"), options));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("blob", false)]
    [InlineData("container", false)]
    public async Task ReportsPrivacyWithoutListingOrReadingObjects(string? access, bool expected)
    {
        var transport = new ReadOnlyTransport(access, "<DeleteRetentionPolicy><Enabled>true</Enabled><Days>7</Days></DeleteRetentionPolicy>");
        var report = await Inspector(transport).InspectAsync("files", TimeSpan.FromSeconds(5));
        Assert.Equal(expected, report.ContainerIsPrivate);
        Assert.True(report.BlobSoftDeleteEnabled);
        Assert.Equal(7, report.BlobSoftDeleteDays);
        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Get }, transport.Methods);
    }

    [Fact]
    public async Task MissingRetentionIsUnknownNotDisabled()
    {
        var transport = new ReadOnlyTransport();
        var report = await Inspector(transport).InspectAsync("files", TimeSpan.FromSeconds(5));
        Assert.Null(report.BlobSoftDeleteEnabled); Assert.Null(report.BlobSoftDeleteDays);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task InaccessibleContainerCannotBeReportedSafe(HttpStatusCode status)
    {
        var transport = new ReadOnlyTransport { Failure = status };
        await Assert.ThrowsAsync<Azure.RequestFailedException>(() => Inspector(transport).InspectAsync("files", TimeSpan.FromSeconds(5)));
        Assert.Single(transport.Methods);
    }

    [Fact]
    public async Task CancellationTimeoutAndInvalidBudgetDoNotCauseWrites()
    {
        var transport = new ReadOnlyTransport { Hang = true };
        var inspector = Inspector(transport);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inspector.InspectAsync("files", TimeSpan.FromSeconds(5), cancelled.Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => inspector.InspectAsync("files", TimeSpan.Zero));
        Assert.Empty(transport.Methods);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inspector.InspectAsync("files", TimeSpan.FromMilliseconds(30)));
        Assert.Single(transport.Methods);
    }
}

