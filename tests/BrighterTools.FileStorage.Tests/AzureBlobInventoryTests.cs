using BrighterTools.FileStorage.AzureBlob;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BrighterTools.FileStorage.Tests;

public class AzureBlobInventoryTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/")]
    [InlineData("../outside")]
    public async Task InvalidRootsAreRejectedBeforeAnyStorageRequest(string root)
    {
        var service = new AzureBlobFileStorageService(Options.Create(new AzureBlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true", ContainerName = "files",
            AccountName = "devstoreaccount1", AccountKey = "fixture"
        }), NullLogger<AzureBlobFileStorageService>.Instance);
        await using var files = service.EnumerateFiles(root, "files").GetAsyncEnumerator();
        await Assert.ThrowsAsync<ArgumentException>(async () => await files.MoveNextAsync());
    }
}