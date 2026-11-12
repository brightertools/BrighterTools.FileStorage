using System.Security.Cryptography;
using System.Text;

namespace BrighterTools.FileStorage.Disk;

/// <summary>Opt-in private disk provider. Keys are immutable; public delivery and authorization belong to the host.</summary>
public sealed class DiskFileStorage : IWritableFileStorage, IStreamingFileStorage, IBoundedFileStorage
{
    private readonly string root;
    public DiskFileStorage(string rootPath)
    {
        if (!Path.IsPathFullyQualified(rootPath)) throw new ArgumentException("Storage root must be absolute.");
        root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }
    private string Resolve(string key, string container)
    {
        var parts = (container + "/" + key).Split('/');
        if (parts.Any(x => x.Length == 0 || x is "." or ".." || x.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.')))
            throw new ArgumentException("Invalid storage key.");
        var path = Path.GetFullPath(Path.Combine(root, Path.Combine(parts)));
        if (!path.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new ArgumentException("Invalid storage key.");
        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked storage paths are not allowed.");
        return path;
    }
    private static string Mime(string path) => Path.GetExtension(path).ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", ".pdf" => "application/pdf", _ => "application/octet-stream" };
    private static StorageObjectMetadata Metadata(string path)
    {
        var file = new FileInfo(path); if (!file.Exists) throw new FileNotFoundException();
        var etag = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(file.Length + ":" + file.LastWriteTimeUtc.Ticks)));
        return new(file.Length, '"' + etag + '"', file.LastWriteTimeUtc, Mime(path));
    }
    public async Task<StorageObjectMetadata> CreateAsync(string path, string container, Stream source, string contentType, long maximumBytes, CancellationToken cancellationToken = default)
    {
        var destination = Resolve(path, container); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true)) {
                var buffer = new byte[81920]; long count = 0; int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0) {
                    count += read; if (count > maximumBytes) throw new IOException("File exceeds the upload limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                if (count == 0) throw new IOException("File is empty.");
            }
            cancellationToken.ThrowIfCancellationRequested(); File.Move(temporary, destination, false); return Metadata(destination);
        } finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public Task<StorageObjectMetadata> ReadMetadataAsync(string path, string container, TimeSpan timeout, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Metadata(Resolve(path, container))); }
    public Task<Stream> OpenReadRangeAsync(string path, string container, StorageObjectMetadata observed, long offset, long length, TimeSpan idleTimeout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var file = Resolve(path, container);
        var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        try {
            if (Metadata(file) != observed || offset < 0 || length < 0 || offset > observed.Length - length) throw new IOException("The file changed or the range is invalid.");
            stream.Position = offset; return Task.FromResult<Stream>(new LimitedReadStream(stream, length));
        } catch { stream.Dispose(); throw; }
    }
    public async Task<byte[]> DownloadBoundedAsync(string path, string container, long maxBytes, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); deadline.CancelAfter(timeout);
        var meta = await ReadMetadataAsync(path, container, timeout, deadline.Token);
        if (meta.Length > maxBytes) throw new IOException("File exceeds the download limit.");
        await using var source = await OpenReadRangeAsync(path, container, meta, 0, meta.Length, timeout, deadline.Token);
        using var output = new MemoryStream(); await source.CopyToAsync(output, deadline.Token); return output.ToArray();
    }
    private sealed class LimitedReadStream(Stream inner, long remaining) : Stream
    {
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) { var read = inner.Read(buffer, offset, (int)Math.Min(count, remaining)); remaining -= read; return read; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) { var read = await inner.ReadAsync(buffer[..(int)Math.Min(buffer.Length, remaining)], ct); remaining -= read; return read; }
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override ValueTask DisposeAsync() => inner.DisposeAsync();
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
