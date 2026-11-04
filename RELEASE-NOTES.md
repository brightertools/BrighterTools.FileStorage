# Release 1.1.0 — published 4 November 2026

Both BrighterTools.FileStorage and BrighterTools.FileStorage.AzureBlob 1.1.0
are published on NuGet. Trusted publishing run: https://github.com/brightertools/BrighterTools.FileStorage/actions/runs/41847682873.

- Release the previously committed streaming, bounded-transfer, immutable-write,
  inventory, transport-identity and Azure property inspection capabilities.
- Add IPrivateFileStorage.EnsurePrivateAsync: read-only verification that a
  container exists and denies anonymous access, with cancellation/deadline support.
- Registration no longer requires duplicate account name/key credentials for
  ordinary storage access. Shared-key signed URLs check their credentials on use.
- Update Azure.Storage.Blobs to 12.30.1, Azure.Core to 1.64.0 and MimeKit to 4.18.2.
  Azure.Storage.Common resolves to 12.29.0. Azure.Identity is host-owned (latest
  stable 1.21.0), not needed for connection-string access.
- Existing buffered methods and signatures remain available. New streaming APIs
  use exact keys, never auto-create containers and avoid whole-file buffering.

Validation: Release test suite, transitive vulnerability audit, NuGet packing and
Adfast package-consumer regression tests. See the host handoff for final counts.
Transfers use isolated fake transports; no production account operations were run.

Publish with .github/workflows/publish-tool.yml on the reviewed release commit,
using the existing trusted-publishing production environment. No source push,
workflow dispatch or public package publication has been performed in this update.

# Release 1.0.1 — 2026-09-17

This release updates the existing library; SQL Server defaults and host-owned
persistence registration remain unchanged. The proposed production SQLite
expansion is cancelled. Existing SQLite samples/tests do not establish a new
production-support commitment.

## Changes

- Update compatible stable dependencies and .NET 10 servicing packages.
- Preserve existing BrighterTools integrations except where explicitly noted below.
- Validate Release builds and packages before publication.

## Publication

Use `.github/workflows/publish-tool.yml` on the release commit. The workflow
validates before publishing and uses the production environment with registry
trusted publishing. Registry policies must authorize this repository and workflow;
a locally built package is not proof of successful publication.
