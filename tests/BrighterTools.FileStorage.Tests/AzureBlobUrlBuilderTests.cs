using BrighterTools.FileStorage.AzureBlob;
using FluentAssertions;

namespace BrighterTools.FileStorage.Tests;

public class AzureBlobUrlBuilderTests
{
    [Fact]
    public void BuildPublicUrl_BuildsExpectedUrl()
    {
        var url = AzureBlobUrlBuilder.BuildPublicUrl("https://cdn.example.com", "files", "tenant/file.png");

        url.Should().Be("https://cdn.example.com/files/tenant/file.png");
    }
}
