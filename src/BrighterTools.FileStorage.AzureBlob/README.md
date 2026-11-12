# BrighterTools.FileStorage.AzureBlob

Azure Blob provider for the BrighterTools.FileStorage abstractions. Version 1.1.0
adds cancellable streaming, exact-version reads, immutable streaming writes,
recursive inventory and private-container validation. Existing buffered APIs remain
available for compatibility.

```csharp
services.AddBrighterToolsAzureBlobFileStorage(configuration);
```

```json
{
  "AzureBlobStorage": {
    "ConnectionString": "<secret>",
    "ContainerName": "private-files"
  }
}
```

Only ConnectionString and ContainerName are required. AccountName/AccountKey are
needed only for the older shared-key GetSignedUrl API. PublicBaseUrl is optional;
API-proxied private files do not need a public endpoint, CDN or signed storage URL.
Use the explicit-client constructor for a host-owned TokenCredential/managed-identity
BlobServiceClient; Azure.Identity belongs to that host when required.

## Private API-served files

1. The host authorizes a logical artifact identifier using its database ownership rules.
2. IPrivateFileStorage.EnsurePrivateAsync verifies an existing private container.
   This only reads container properties; it never creates a container or changes ACLs.
3. For uploads use IImmutableStreamFileStorage.CreateImmutableStreamAsync with the
   exact object key, length, content type, cancellation token and operation timeout.
   Buffer use is bounded, the caller retains stream ownership, and existing keys
   cannot be overwritten. False means an existing key, not a verified upload.
4. For reads use IStreamingFileStorage.ReadMetadataAsync followed by
   OpenReadRangeAsync using that observed metadata. Reads are ETag-pinned, have
   exact range validation, cancellation and an inactivity timeout. Dispose the
   returned stream. No local temporary download or whole-file buffer is needed.

The host owns HTTP status/range/cache semantics, file naming and authorization.
Container privacy alone does not authorize users. A GUID is not an access token.
The streaming APIs preserve exact object keys and require pre-existing containers.
The old UploadFile API still normalizes names and buffers for backward compatibility.

Azure dependencies: Azure.Storage.Blobs 12.30.1, Azure.Storage.Common 12.29.0
(transitive), Azure.Core 1.64.0. Verified against NuGet on 4 November 2026.