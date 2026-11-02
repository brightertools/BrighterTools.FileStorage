using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace BrighterTools.FileStorage.AzureBlob;

/// <summary>Observed data-plane settings only; not a lifecycle, CDN or historical-erasure certification.</summary>
public sealed record AzureBlobStorageInspection(
    DateTimeOffset ObservedAtUtc, bool ContainerIsPrivate, bool? HasLegalHold,
    bool? HasImmutabilityPolicy, bool? BlobSoftDeleteEnabled, int? BlobSoftDeleteDays);

/// <summary>Read-only container/service inspection. No blob enumeration, content reads or mutations.</summary>
public sealed class AzureBlobStorageInspector(BlobServiceClient client)
{
    /// <summary>Reads container and service properties. Unavailable facts fail rather than implying safety.</summary>
    public async Task<AzureBlobStorageInspection> InspectAsync(string container, TimeSpan timeout, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(container)) throw new ArgumentException("A container is required.", nameof(container));
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(2)) throw new ArgumentOutOfRangeException(nameof(timeout));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        deadline.Token.ThrowIfCancellationRequested();
        var properties = (await client.GetBlobContainerClient(container).GetPropertiesAsync(cancellationToken: deadline.Token)).Value;
        var service = (await client.GetPropertiesAsync(deadline.Token)).Value;
        return new(DateTimeOffset.UtcNow, properties.PublicAccess == PublicAccessType.None,
            properties.HasLegalHold, properties.HasImmutabilityPolicy,
            service.DeleteRetentionPolicy?.Enabled, service.DeleteRetentionPolicy?.Days);
    }
}

