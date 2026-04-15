# BrighterTools.FileStorage Integration

## Overview

`BrighterTools.FileStorage` is the provider-agnostic storage core.

It owns:
- storage abstractions
- storage metadata models
- optional tree-building helpers

It does not own:
- business file lifecycle rules
- tenant naming conventions
- public versus signed URL policy
- provider credential storage
- concrete cloud provider registration

Concrete providers are registered through companion packages.

## Core Registration

The core package is abstraction-only. Register the provider package explicitly in the host application.

```csharp
services.AddBrighterToolsAzureBlobFileStorage(configuration);
```

## Current Provider Package

- `BrighterTools.FileStorage.AzureBlob`
  - Azure Blob Storage implementation
  - signed URL generation
  - tag management
  - public URL generation using a configured public base URL

## Azure Blob Configuration

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

`PublicBaseUrl` is optional. If it is configured, `GetFileUrl(...)` returns a public URL rooted there. Otherwise it returns the blob URI.

## Host Usage Pattern

Keep business-specific rules in the consuming app:
- pending file workflows
- thumbnail and preview generation
- deletion policy
- CDN/public access policy
- path/category construction

Use the storage package only for persistence and access URL generation.
