using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace BrighterTools.FileStorage.AzureBlob;

public partial class AzureBlobFileStorageService
{
    private const int MaxBufferedTransfer = 256 * 1024 * 1024;
    private const string ProtocolMetadata = "btstorage";
    private const string SealedProtocol = "sealed-v1";

    /// <inheritdoc />
    public async Task<byte[]> DownloadBoundedAsync(string path, string container, long maxBytes, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTransferPath(path);
        if (maxBytes <= 0 || maxBytes > MaxBufferedTransfer) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        using var deadline = TransferDeadline(timeout, cancellationToken);
        var ct = deadline.Token;
        ct.ThrowIfCancellationRequested();
        var blob = _blobServiceClient.GetBlobContainerClient(container).GetBlobClient(path);
        try
        {
            var properties = (await blob.GetPropertiesAsync(cancellationToken: ct)).Value;
            var length = properties.ContentLength;
            if (length < 0 || length > maxBytes) throw new InvalidDataException("Stored object exceeds the read budget.");
            if (length == 0) return [];
            // Pin the version and range before allocation; never buffer an unchecked response.
            var response = await blob.DownloadStreamingAsync(new BlobDownloadOptions
            { Conditions = new BlobRequestConditions { IfMatch = properties.ETag }, Range = new HttpRange(0, length) }, ct);
            await using var body = response.Value.Content;
            if (response.Value.Details.ContentLength != length) throw new InvalidDataException("Stored object length changed.");
            var bytes = new byte[checked((int)length)];
            var offset = 0;
            while (offset < bytes.Length)
            {
                var count = await body.ReadAsync(bytes.AsMemory(offset), ct);
                if (count == 0) throw new InvalidDataException("Stored object was truncated.");
                offset += count;
            }
            if (await body.ReadAsync(new byte[1], ct) != 0) throw new InvalidDataException("Stored object exceeds its declared length.");
            return bytes;
        }
        catch (RequestFailedException e) when (e.Status == 404) { throw new FileNotFoundException("Stored object not found."); }
    }

    /// <inheritdoc />
    public async Task<bool> CreateImmutableAsync(string path, string container, byte[] content, string contentType, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTransferPath(path);
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0 || content.Length > MaxBufferedTransfer) throw new ArgumentOutOfRangeException(nameof(content));
        if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentException("A content type is required.", nameof(contentType));
        using var deadline = TransferDeadline(timeout, cancellationToken);
        deadline.Token.ThrowIfCancellationRequested();
        using var stream = new MemoryStream(content, writable: false);
        try
        {
            // BlockBlobClient performs a single Put Blob, not a staged multi-part upload.
            await _blobServiceClient.GetBlobContainerClient(container).GetBlockBlobClient(path).UploadAsync(stream,
                new BlobUploadOptions { Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType, CacheControl = "private, no-store" },
                    Metadata = new Dictionary<string, string> { [ProtocolMetadata] = "immutable-v1" } }, deadline.Token);
            return true;
        }
        catch (RequestFailedException e) when (e.Status == 412 || e.Status == 409 && e.ErrorCode == "BlobAlreadyExists") { return false; }
    }

    /// <inheritdoc />
    public async Task SealAsync(string path, string container, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTransferPath(path);
        using var deadline = TransferDeadline(timeout, cancellationToken);
        deadline.Token.ThrowIfCancellationRequested();
        using var empty = new MemoryStream(Array.Empty<byte>(), writable: false);
        await _blobServiceClient.GetBlobContainerClient(container).GetBlockBlobClient(path).UploadAsync(empty,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/octet-stream", CacheControl = "private, no-store" },
                Metadata = new Dictionary<string, string> { [ProtocolMetadata] = SealedProtocol } }, deadline.Token);
    }

    /// <inheritdoc />
    public async Task<bool> IsSealedAsync(string path, string container, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTransferPath(path);
        using var deadline = TransferDeadline(timeout, cancellationToken);
        deadline.Token.ThrowIfCancellationRequested();
        try
        {
            var p = (await _blobServiceClient.GetBlobContainerClient(container).GetBlobClient(path).GetPropertiesAsync(cancellationToken: deadline.Token)).Value;
            return p.ContentLength == 0 && p.Metadata.TryGetValue(ProtocolMetadata, out var protocol) && protocol == SealedProtocol;
        }
        catch (RequestFailedException e) when (e.Status == 404) { return false; }
    }

    private static CancellationTokenSource TransferDeadline(TimeSpan timeout, CancellationToken ct)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(10)) throw new ArgumentOutOfRangeException(nameof(timeout));
        var source = CancellationTokenSource.CreateLinkedTokenSource(ct);
        source.CancelAfter(timeout);
        return source;
    }

    private static void ValidateTransferPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 1000 || path.Contains((char)92) || path.Contains('%') ||
            path.Contains('?') || path.Contains('#') || path.Contains(':') || path.Any(char.IsControl) ||
            path.Split('/').Any(x => x is "" or "." or "..")) throw new ArgumentException("A safe relative object path is required.", nameof(path));
    }
}

