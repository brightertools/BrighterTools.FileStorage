# BrighterTools.FileStorage.AzureBlob

Registers `IFileStorageService` using Azure Blob Storage.

## Package

```powershell
dotnet add package BrighterTools.FileStorage.AzureBlob
```

## Registration

```csharp
services.AddBrighterToolsAzureBlobFileStorage(configuration);
```

## Configuration

```json
{
  "AzureBlobStorage": {
    "ConnectionString": "...",
    "ContainerName": "files",
    "AccountName": "storage-account",
    "AccountKey": "storage-key",
    "MaxBlobPathLength": 1000,
    "PublicBaseUrl": "https://cdn.example.com"
  }
}
```
