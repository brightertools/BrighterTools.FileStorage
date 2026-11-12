# BrighterTools.FileStorage

`BrighterTools.FileStorage` contains provider-agnostic abstractions for file storage.

This package provides:
- `IFileStorageService`
- storage metadata models
- storage tree helper utilities

This package does not register a concrete backend. Add a provider package explicitly, such as:
- `BrighterTools.FileStorage.AzureBlob`

## Package

```powershell
dotnet add package BrighterTools.FileStorage
```

Version 1.1.0 also includes optional IPrivateFileStorage, IStreamingFileStorage,
IImmutableStreamFileStorage, IBoundedFileStorage, IImmutableFileStorage,
IFileStorageInventory and IFileStorageIdentity capabilities. Provider packages
implement these primitives; hosts retain authorization and lifecycle policy.
