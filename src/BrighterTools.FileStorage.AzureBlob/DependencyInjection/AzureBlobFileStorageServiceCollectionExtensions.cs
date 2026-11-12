using BrighterTools.FileStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BrighterTools.FileStorage.AzureBlob.DependencyInjection;

/// <summary>
/// Provides extension methods for Azure Blob File Storage Service Collection.
/// </summary>
public static class AzureBlobFileStorageServiceCollectionExtensions
{
    /// <summary>
    /// Adds Brighter Tools Azure Blob File Storage.
    /// </summary>
    public static IServiceCollection AddBrighterToolsAzureBlobFileStorage(this IServiceCollection services, IConfiguration configuration, string sectionName = "AzureBlobStorage")
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<AzureBlobStorageOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), $"{sectionName}:ConnectionString is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ContainerName), $"{sectionName}:ContainerName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.AccountName), $"{sectionName}:AccountName is required.")
            .Validate(options => options.MaxBlobPathLength > 0, $"{sectionName}:MaxBlobPathLength must be greater than zero.")
            .ValidateOnStart();

        // Keep storage credentials owned by this section, not an unrelated host BlobServiceClient.
        services.AddSingleton<IFileStorageService>(sp => new AzureBlobFileStorageService(
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureBlobStorageOptions>>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AzureBlobFileStorageService>>()));
        services.AddSingleton<IFileStorageInventory>(sp => (IFileStorageInventory)sp.GetRequiredService<IFileStorageService>());
        services.AddSingleton<IBoundedFileStorage>(sp => (IBoundedFileStorage)sp.GetRequiredService<IFileStorageService>());
        services.AddSingleton<IImmutableFileStorage>(sp => (IImmutableFileStorage)sp.GetRequiredService<IFileStorageService>());
        services.AddSingleton<IImmutableStreamFileStorage>(sp => (IImmutableStreamFileStorage)sp.GetRequiredService<IFileStorageService>());
        services.AddSingleton<IStreamingFileStorage>(sp => (IStreamingFileStorage)sp.GetRequiredService<IFileStorageService>());
        services.AddSingleton<IFileStorageIdentity>(sp => (IFileStorageIdentity)sp.GetRequiredService<IFileStorageService>());
        return services;
    }
}
