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
            .Validate(options => !string.IsNullOrWhiteSpace(options.AccountKey), $"{sectionName}:AccountKey is required.")
            .Validate(options => options.MaxBlobPathLength > 0, $"{sectionName}:MaxBlobPathLength must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<IFileStorageService, AzureBlobFileStorageService>();
        return services;
    }
}

