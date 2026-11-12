# Shared storage release handoff — 12 November 2026

Prepared package version: 1.3.0 for BrighterTools.FileStorage and BrighterTools.FileStorage.AzureBlob. The five incoming main commits through a78f536 were reconciled with the existing Mac changes, retaining private-container checks, version-pinned copy, bounded deletion and the opt-in disk provider source.

Container-scoped SAS credentials can register and perform streaming/inventory operations without duplicating AccountName/AccountKey. Only legacy shared-key signed-URL generation requires both, before issuing any storage request. Inventory lists the exact prefix directly; only 404/ContainerNotFound represents an absent container. Authorization errors propagate and never imply an empty inventory.

Azure dependencies retain the newer upstream versions. Pkcs is pinned to 10.0.13. The new IWritableFileStorage contract is additive. The Disk project explicitly remains IsPackable=false as upstream intended: it is source-only and is not a new public NuGet package in this release. It is not substituted for any production Azure destination.

Hosts continue owning authorization, private storage configuration, quota accounting, durable receipts and recovery. No credentials, containers, ACLs, production media or app deployments are changed by this release. Earlier isolated Azure acceptance does not assert acceptance of every consumer's new storage relocation policy.

Use the existing publish-tool.yml workflow with NuGet trusted publishing and verify both package indexes after success. Validation and publication evidence follow below.

## Local release validation

12 November 2026: Release tests 82 passed, 0 skipped; full solution build (including source-only Disk) and packing passed. Two package pairs were produced: core and AzureBlob 1.3.0. Five existing Disk XML documentation warnings remain. Transitive restore audit treated NU1901–NU1904 as errors. A repository-local public-only NuGet.config now prevents inherited machine-specific feeds from affecting reproducibility (the initial attempt encountered an unrelated Telerik 401).

Commands: `dotnet test BrighterTools.FileStorage.sln -c Release -m:1 -p:NuGetAuditMode=all -warnaserror:NU1901,NU1902,NU1903,NU1904`; `dotnet build BrighterTools.FileStorage.sln -c Release --no-restore -m:1`; `dotnet pack BrighterTools.FileStorage.sln -c Release --no-build`. These tests use controlled transports; no production storage mutation was performed for this release.
