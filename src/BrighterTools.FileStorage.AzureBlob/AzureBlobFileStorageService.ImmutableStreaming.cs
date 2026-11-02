using System.Buffers;
using System.Globalization;
using System.Text;
using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace BrighterTools.FileStorage.AzureBlob;

public partial class AzureBlobFileStorageService : IImmutableStreamFileStorage
{
    private const int ImmutableBlockBytes = 4 * 1024 * 1024;
    private const int MaximumImmutableBlocks = 50000;

    /// <inheritdoc />
    public async Task<bool> CreateImmutableStreamAsync(string path, string container, Stream content, long length,
        string contentType, TimeSpan operationTimeout, CancellationToken cancellationToken = default)
    {
        ValidateTransferPath(path);
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead) throw new ArgumentException("A readable stream is required.", nameof(content));
        if (length <= 0 || length > (long)ImmutableBlockBytes * MaximumImmutableBlocks)
            throw new ArgumentOutOfRangeException(nameof(length));
        if (content.CanSeek && content.Length - content.Position != length)
            throw new InvalidDataException("Upload length differs from the source stream.");
        if (string.IsNullOrWhiteSpace(contentType) || contentType.Any(char.IsControl))
            throw new ArgumentException("A safe content type is required.", nameof(contentType));
        using (var check = TransferDeadline(operationTimeout, cancellationToken)) check.Token.ThrowIfCancellationRequested();
        var blob = _blobServiceClient.GetBlobContainerClient(container).GetBlockBlobClient(path);
        var attempt = Guid.NewGuid().ToString("N");
        var ids = new List<string>();
        var buffer = ArrayPool<byte>.Shared.Rent(ImmutableBlockBytes);
        try
        {
            long remaining = length;
            while (remaining > 0)
            {
                var count = (int)Math.Min(remaining, ImmutableBlockBytes);
                var read = 0;
                while (read < count)
                {
                    using var deadline = TransferDeadline(operationTimeout, cancellationToken);
                    var next = await content.ReadAsync(buffer.AsMemory(read, Math.Min(count - read, 64 * 1024)), deadline.Token);
                    if (next == 0) throw new InvalidDataException("Upload stream was truncated.");
                    read += next;
                }
                // Attempt-specific, fixed-length IDs isolate concurrent writers of the same object.
                var id = Convert.ToBase64String(Encoding.ASCII.GetBytes(attempt + ids.Count.ToString("D5", CultureInfo.InvariantCulture)));
                using var block = new MemoryStream(buffer, 0, count, writable: false);
                using var blockDeadline = TransferDeadline(operationTimeout, cancellationToken);
                await blob.StageBlockAsync(id, block, cancellationToken: blockDeadline.Token);
                ids.Add(id);
                remaining -= count;
            }
            using (var deadline = TransferDeadline(operationTimeout, cancellationToken))
                if (await content.ReadAsync(buffer.AsMemory(0, 1), deadline.Token) != 0)
                    throw new InvalidDataException("Upload stream exceeds its declared length.");
            using var commitDeadline = TransferDeadline(operationTimeout, cancellationToken);
            try
            {
                await blob.CommitBlockListAsync(ids, new CommitBlockListOptions
                {
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType, CacheControl = "private, no-store" },
                    Metadata = new Dictionary<string, string> { [ProtocolMetadata] = "immutable-v1" }
                }, commitDeadline.Token);
                return true;
            }
            catch (RequestFailedException e) when (e.Status == 412 || e.Status == 409 && e.ErrorCode == "BlobAlreadyExists")
            { return false; }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }
}
