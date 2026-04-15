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
