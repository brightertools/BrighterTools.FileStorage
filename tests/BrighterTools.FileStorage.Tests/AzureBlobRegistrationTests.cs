using BrighterTools.FileStorage;
using BrighterTools.FileStorage.AzureBlob.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BrighterTools.FileStorage.Tests;

public class AzureBlobRegistrationTests
{
    [Fact]
    public void AddBrighterToolsAzureBlobFileStorage_RegistersFileStorageService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureBlobStorage:ConnectionString"] = "UseDevelopmentStorage=true",
                ["AzureBlobStorage:ContainerName"] = "files",
                ["AzureBlobStorage:MaxBlobPathLength"] = "1000"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>));
        services.AddBrighterToolsAzureBlobFileStorage(configuration);

        services.Should().ContainSingle(x => x.ServiceType == typeof(IFileStorageService));
        services.Should().ContainSingle(x => x.ServiceType == typeof(IFileStorageInventory));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var storage = provider.GetRequiredService<IFileStorageService>();
        provider.GetRequiredService<IFileStorageInventory>().Should().BeSameAs(storage);
        provider.GetRequiredService<IBoundedFileStorage>().Should().BeSameAs(storage);
        provider.GetRequiredService<IImmutableFileStorage>().Should().BeSameAs(storage);
        provider.GetRequiredService<IImmutableStreamFileStorage>().Should().BeSameAs(storage);
        provider.GetRequiredService<IImmutableObjectCopyStorage>().Should().BeSameAs(storage);
        provider.GetRequiredService<IStreamingFileStorage>().Should().BeSameAs(storage);
        provider.GetRequiredService<IPrivateFileStorage>().Should().BeSameAs(storage);
        provider.GetRequiredService<IFileStorageIdentity>().Should().BeSameAs(storage);
        provider.GetRequiredService<IFileStorageIdentity>().ServiceEndpoint.AbsoluteUri.Should().Be("http://127.0.0.1:10000/devstoreaccount1");
    }
}
