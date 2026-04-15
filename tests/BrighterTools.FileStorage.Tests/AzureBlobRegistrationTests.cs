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
                ["AzureBlobStorage:AccountName"] = "devstoreaccount1",
                ["AzureBlobStorage:AccountKey"] = "key",
                ["AzureBlobStorage:MaxBlobPathLength"] = "1000"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddBrighterToolsAzureBlobFileStorage(configuration);

        services.Should().ContainSingle(x => x.ServiceType == typeof(IFileStorageService));
    }
}
