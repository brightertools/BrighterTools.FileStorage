# Shared storage release handoff — 12 November 2026

Prepared package version: 1.3.0 for BrighterTools.FileStorage and BrighterTools.FileStorage.AzureBlob. The five incoming main commits through a78f536 were reconciled with the existing Mac changes, retaining private-container checks, version-pinned copy, bounded deletion and the opt-in disk provider source.

Container-scoped SAS credentials can register and perform streaming/inventory operations without duplicating AccountName/AccountKey. Only legacy shared-key signed-URL generation requires both, before issuing any storage request. Inventory lists the exact prefix directly; only 404/ContainerNotFound represents an absent container. Authorization errors propagate and never imply an empty inventory.

Azure dependencies retain the newer upstream versions. Pkcs is pinned to 10.0.13. The new IWritableFileStorage contract is additive. The Disk project explicitly remains IsPackable=false as upstream intended: it is source-only and is not a new public NuGet package in this release. It is not substituted for any production Azure destination.

Hosts continue owning authorization, private storage configuration, quota accounting, durable receipts and recovery. No credentials, containers, ACLs, production media or app deployments are changed by this release. Earlier isolated Azure acceptance does not assert acceptance of every consumer's new storage relocation policy.

Use the existing publish-tool.yml workflow with NuGet trusted publishing and verify both package indexes after success. Validation and publication evidence follow below.

## Local release validation

12 November 2026: Release tests 82 passed, 0 skipped; full solution build (including source-only Disk) and packing passed. Two package pairs were produced: core and AzureBlob 1.3.0. Five existing Disk XML documentation warnings remain. Transitive restore audit treated NU1901–NU1904 as errors. A repository-local public-only NuGet.config now prevents inherited machine-specific feeds from affecting reproducibility (the initial attempt encountered an unrelated Telerik 401).

Commands: `dotnet test BrighterTools.FileStorage.sln -c Release -m:1 -p:NuGetAuditMode=all -warnaserror:NU1901,NU1902,NU1903,NU1904`; `dotnet build BrighterTools.FileStorage.sln -c Release --no-restore -m:1`; `dotnet pack BrighterTools.FileStorage.sln -c Release --no-build`. These tests use controlled transports; no production storage mutation was performed for this release.

## Published release

The trusted-publishing workflow completed successfully on 12 November 2026.

- Published source commit: `11bc9cdb9b1ef5ca731bbd4652350fa9fc185803`.
- Workflow: [https://github.com/brightertools/BrighterTools.FileStorage/actions/runs/43246322039](https://github.com/brightertools/BrighterTools.FileStorage/actions/runs/43246322039).
- CI repeated the required tests, audits and package validation before successful registry uploads.
- Registry availability is checked separately below; workflow success alone is not a download check.

CI package SHA-256 (before any registry repository signing):

- `BrighterTools.FileStorage.1.3.0.nupkg`: `837f87fb9c01054d290e633277ed5c5991014a1f09329c6dab4785357d49897d`.
- `BrighterTools.FileStorage.AzureBlob.1.3.0.nupkg`: `b1514cb77f5e5ad5ac601b5d562dde234f2b050999a24ff8d302e21dbaa671ae`.

For rollback, pin consumers to their prior package versions and review host migrations separately; do not delete published packages or undo additive host schema as part of package rollback. Source-reference consumers use their reviewed sibling revisions, not a substituted registry reference.

Registry availability verified at 2026-11-12T15:25:19.995636+00:00: both NuGet 1.3.0 packages downloaded successfully. All archive contents match CI, excluding NuGet repository signatures.

- Published `BrighterTools.FileStorage` SHA-256: `5b8f684fcabbad7a81812c29621f7fbbeeb5774fc95cc794c718dfa19c3727f9`.
- Published `BrighterTools.FileStorage.AzureBlob` SHA-256: `3083ac4fe759b79762aade981248280c545b2f87bbf2dd5269124c470b7150bc`.
