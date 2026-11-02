using Azure;
using Azure.Storage.Blobs.Models;

namespace BrighterTools.FileStorage.AzureBlob;

public partial class AzureBlobFileStorageService : IStreamingFileStorage
{
    /// <inheritdoc />
    public async Task<StorageObjectMetadata> ReadMetadataAsync(string path, string container, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTransferPath(path);
        using var deadline = TransferDeadline(timeout, cancellationToken);
        deadline.Token.ThrowIfCancellationRequested();
        try
        {
            var properties = (await _blobServiceClient.GetBlobContainerClient(container).GetBlobClient(path)
                .GetPropertiesAsync(cancellationToken: deadline.Token)).Value;
            if (properties.Metadata.TryGetValue(ProtocolMetadata, out var protocol) && protocol == SealedProtocol)
                throw new FileNotFoundException("Stored object is retired.");
            if (properties.ContentLength < 0) throw new InvalidDataException("Invalid stored object length.");
            return new(properties.ContentLength, properties.ETag.ToString(), properties.LastModified, properties.ContentType);
        }
        catch (RequestFailedException e) when (e.Status == 404) { throw new FileNotFoundException("Stored object not found."); }
    }

    /// <inheritdoc />
    public async Task<Stream> OpenReadRangeAsync(string path, string container, StorageObjectMetadata observed,
        long offset, long length, TimeSpan idleTimeout, CancellationToken cancellationToken = default)
    {
        ValidateTransferPath(path);
        ArgumentNullException.ThrowIfNull(observed);
        if (observed.Length < 0 || string.IsNullOrWhiteSpace(observed.ETag) || observed.ETag == "*")
            throw new ArgumentException("An exact observed object version is required.", nameof(observed));
        if (offset < 0 || length < 0 || offset > observed.Length || length > observed.Length - offset)
            throw new ArgumentOutOfRangeException(nameof(length));
        using var deadline = TransferDeadline(idleTimeout, cancellationToken);
        deadline.Token.ThrowIfCancellationRequested();
        if (length == 0) return new MemoryStream(Array.Empty<byte>(), writable: false);
        try
        {
            var response = await _blobServiceClient.GetBlobContainerClient(container).GetBlobClient(path).DownloadStreamingAsync(
                new BlobDownloadOptions { Range = new HttpRange(offset, length), Conditions = new BlobRequestConditions { IfMatch = new ETag(observed.ETag) } }, deadline.Token);
            var body = response.Value.Content;
            var details = response.Value.Details;
            if (details.ContentLength != length || details.ETag.ToString() != observed.ETag ||
                details.ContentRange != $"bytes {offset}-{offset + length - 1}/{observed.Length}")
            {
                await body.DisposeAsync();
                throw new InvalidDataException("Storage returned an unexpected object range or version.");
            }
            // The header deadline must not become a fixed total timeout for a long-running download.
            return new ExactStorageReadStream(body, length, idleTimeout, cancellationToken);
        }
        catch (RequestFailedException e) when (e.Status == 404) { throw new FileNotFoundException("Stored object not found."); }
    }

    private sealed class ExactStorageReadStream(Stream inner, long remaining, TimeSpan idleTimeout, CancellationToken requestCancellation) : Stream
    {
        private bool disposed;
        public override bool CanRead => !disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestCancellation, cancellationToken);
            timeout.CancelAfter(idleTimeout);
            timeout.Token.ThrowIfCancellationRequested();
            if (buffer.Length == 0) return 0;
            if (remaining == 0)
            {
                if (await inner.ReadAsync(new byte[1], timeout.Token) != 0) throw new InvalidDataException("Stored object exceeds the requested range.");
                return 0;
            }
            var read = await inner.ReadAsync(buffer[..(int)Math.Min(Math.Min(remaining, buffer.Length), 64 * 1024)], timeout.Token);
            if (read == 0) throw new InvalidDataException("Stored object range was truncated.");
            remaining -= read;
            return read;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (!disposed && disposing) inner.Dispose();
            disposed = true;
            base.Dispose(disposing);
        }
        public override async ValueTask DisposeAsync()
        {
            if (!disposed) await inner.DisposeAsync();
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

