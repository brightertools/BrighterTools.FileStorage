# SQLite compatibility and release plan

Status updated 2026-09-16: the proposed SQLite expansion is CANCELLED at the user's request. SQL Server remains the production target. Existing SQLite samples, relational tests and UTC mappings are retained unchanged. No new provider switch, adapter, migrations assembly or data-transfer tooling is planned. The original proposal below is historical and is not an implementation queue. Release findings below are also historical; consult the latest workspace release review.

## Historical findings (superseded)

This package does not own EF persistence and inherits the application provider. Add a short README compatibility note: SQL Server stays the host default; SQLite is explicit opt-in. No provider adapter or EF dependency belongs here. Retain public APIs and host-owned migration sets. Existing build/test checks and pack pass (where tests exist). Smoke-test external service adapters before release; unit tests do not prove live email, payment or storage integrations.

## Host contract and migration route

Use Database:Provider = SqlServer (default) or Sqlite, plus ConnectionStrings:DefaultConnection. The host must reference the matching EF provider package and call UseSqlServer or UseSqlite; changing a connection string alone is insufficient. Optional provider-specific migrations assembly settings belong to the host.

Keep separate EF migration sets. For data cutover: freeze writes; back up and validate SQLite; apply SQL Server migrations to a clean target; batch-copy with two contexts in foreign-key order, preserving IDs, tenants, timestamps, tokens and encrypted payloads; handle identity insertion and database-generated fields explicitly; validate counts, keys, constraints and representative reads; switch provider and resume writes. Keep a rollback backup and plan reconciliation of subsequent writes. Do not copy __EFMigrationsHistory. There is no shared migration tool planned.

SQLite excludes server-side decimal range/order workloads, schemas, sequences, native DateTimeOffset comparisons and database-generated concurrency tokens without an explicit alternate strategy.

References: [multiple-provider migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers), [SQLite limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations).

## Dependency and release checklist

Tracked dependencies were updated to current stable compatible versions. .NET 10 remains primary; Common additionally retains net9.0. Frontend tooling uses TypeScript 7.0.2, Vitest 5.0.1 and jsdom 30.0.1 where present; develop with Node 24.15+ on the 24.x line. React 18 peer support is retained. MSBuild and OpenAPI remain on compatible versions rather than incompatible newest majors.

Before release: review dependency-major/public API changes; align host dependency pins; run Release build, tests, dependency audits and package checks; choose new non-conflicting NuGet/npm versions; commit only intended files; verify registry permissions. No versions were bumped, commits/pushes/tags made, or packages published during this review.

Do not initiate the superseded SQLite implementation. Retain this record for context only; this is not a production SQLite-support commitment.
