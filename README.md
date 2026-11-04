# BrighterTools.FileStorage

`BrighterTools.FileStorage` provides file storage abstractions for .NET applications, with explicit provider packages for concrete backends.

The host application owns:
- file persistence decisions and naming policy
- container and path conventions
- business-specific file lifecycle rules
- public versus signed URL usage policy
- provider credential storage

The packages in this repo provide:
- reusable file storage abstractions
- storage metadata models
- simple tree helpers
- explicit provider integration for Azure Blob Storage

## Packages

```powershell
dotnet add package BrighterTools.FileStorage
dotnet add package BrighterTools.FileStorage.AzureBlob
```

## Repository Layout

- `src/BrighterTools.FileStorage`
  - provider-agnostic storage abstractions and helper types
- `src/BrighterTools.FileStorage.AzureBlob`
  - Azure Blob Storage implementation and DI registration
- `tests/BrighterTools.FileStorage.Tests`
  - package-level behavior and registration tests
- `docs`
  - public integration documentation

## Validation

```powershell
dotnet test .\BrighterTools.FileStorage.sln
```

## Documentation

- [`docs/README.md`](docs/README.md)
- [`docs/integration-guide.md`](docs/integration-guide.md)

## Optional Recursive Inventory

IFileStorageInventory.EnumerateFiles(rootPath, containerName, cancellationToken)
streams current objects recursively beneath a relative folder prefix. Azure
registration exposes this optional interface on the existing storage singleton.
It uses flat blob enumeration and does not create missing containers. Existing
IFileStorageService.ListFilesInFolder hierarchy semantics are unchanged.

Inventory is not authorization: hosts must derive the allowed root from trusted
ownership and validate returned paths before destructive actions. It enumerates
current blobs, not historical versions, snapshots, soft-deleted copies or caches.
The library does not implement tenant lifecycle or background cleanup policy.
## Optional Bounded Transfers And Immutable Objects

IBoundedFileStorage provides size-budgeted, cancellable reads with explicit deadlines.
Azure checks length before allocation, pins the ETag and verifies the exact body length.
The maximum buffered transfer is 256 MiB; deadlines must be positive and at most ten
minutes. Existing buffered upload/download interfaces remain unchanged.

IImmutableFileStorage provides single-request create-only writes and permanent zero-byte
seals. CreateImmutableAsync never overwrites an object (If-None-Match: *); false means
the path already exists, not that its contents have been validated. The host must verify
the content/checksum before publishing a reference. SealAsync replaces a path with an
empty marker; IsSealedAsync checks metadata AND zero length. All optional operations
require existing containers and safe relative paths; they do not auto-create containers.

A seal is a permanent remote write fence, not an ordinary temporary blob. Never delete
or expire it: doing so allows a delayed create-only request to recreate the payload.
The host must seal every possible output path, including absent objects, before releasing
capacity, and keep seals during tenant retirement. Reserve this namespace from legacy
unconditional uploads/deletes and exclude it from storage lifecycle deletion policies.
Existing generic methods intentionally remain backward-compatible and do not enforce
this protocol. Provider versioning/soft-delete and historical-copy purge need their own
policy; sealing the current blob does not erase previous versions.

These capabilities are opt-in primitives, not a scheduler or application lifecycle engine.
Registration exposes them on the same singleton configured by AzureBlobStorage. The
explicit Azure-client constructor supports isolated transports; normal DI uses the
configured connection string rather than an unrelated host BlobServiceClient.

## Read-Only Azure Property Inspection

AzureBlobStorageInspector is an explicit operator primitive, not a startup service.
It reads container properties and blob-service properties with cancellation/deadlines,
returning container access, legal-hold/immutability flags and blob soft-delete settings.
Missing retention/hold information remains unknown. It performs no blob enumeration,
content reads, writes, deletion or container creation. Inaccessible properties throw;
they cannot be interpreted as safe/private defaults.

This is not a readiness certificate. Account-level public-access overrides, lifecycle
deletion exclusions, versioning/history, CDN caches, signed-link behavior and other
writers require separate verification. Hosts must redact provider/configuration exception
details before displaying an operator report.

## Streaming delivery reads

`IStreamingFileStorage` is an optional capability implemented by the Azure provider
and registered as an alias of the existing storage singleton. Existing byte-array,
URL, upload and inventory interfaces are unchanged.

Call `ReadMetadataAsync`, authorize the resource in the consuming application,
then call `OpenReadRangeAsync` with the observed metadata and the exact byte span.
The read uses If-Match, checks the returned Content-Range/version/length and rejects
retired markers. The caller owns the returned stream and must dispose it. Reads
are capped to 64 KiB per call, use request cancellation and an inactivity timeout,
not a fixed total download duration. Storage authorization, HTTP semantics and
safe public publication remain host responsibilities. These methods never create
containers or generate public/SAS URLs.

## Transport identity

`IFileStorageIdentity` optionally reports the actual provider service endpoint,
without credentials, SAS query parameters or fragments. Azure registers it on the
same singleton as the transfer interfaces. It is independent of CDN/public URL
settings and does not make a network request. Hosts may compare it with persisted
storage provenance to refuse silent destination changes. It does not verify ACLs,
authorize an operation, or provide a customer delivery URL.

## Create-only streaming uploads

`IImmutableStreamFileStorage` is optional and registered on the existing Azure singleton.
Existing upload APIs are unchanged. Hosts must persist the target namespace/file intent,
reserve quota and establish any cleanup/work fence before calling it. Containers must
already exist; the method neither creates them nor changes access levels.

`CreateImmutableStreamAsync` accepts a readable stream and exact positive length.
The Azure implementation stages at most 4 MiB per block with unique per-call block IDs,
then commits only if the blob does not exist. It does not replace a payload or permanent
seal. False means a create conflict, not a matching upload: the host must verify the
existing object. Cancellation, timeouts or lost responses are uncertain; do not infer
absence, refund quota or delete a safety marker. Source streams remain caller-owned.
The operation timeout applies separately to source reads, block requests and commitment.
The implementation supports at most 50,000 blocks; hosts impose smaller product quotas.

Azure may retain uncommitted blocks after a failed/cancelled operation. They are distinct
from committed file availability, soft-deleted versions and permanent safety markers.
The host must account for this in recovery and retention documentation; this library does
not claim immediate physical purge or implement a cleanup scheduler. See the
[Azure Put Block List contract](https://learn.microsoft.com/en-us/rest/api/storageservices/put-block-list).

Verification uses mocked HTTP transport for bounded blocks, exact byte lengths, unique
block IDs, conditional commitment, existing objects, seals, delayed writes, cancellation
and response loss. Real Azure/load acceptance remains the consuming host's responsibility.


## Private container verification

IPrivateFileStorage.EnsurePrivateAsync validates an existing container before a
private-file transfer. It reads container properties only, rejects missing/public/
inaccessible containers, and observes cancellation and deadlines. The host owns
user authorization and HTTP delivery. No container creation or ACL changes occur.

See RELEASE-NOTES.md for the prepared 1.1.0 release and Azure dependency versions.
