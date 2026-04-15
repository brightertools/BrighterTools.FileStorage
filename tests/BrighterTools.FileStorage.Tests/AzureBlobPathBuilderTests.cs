using BrighterTools.FileStorage.AzureBlob;
using FluentAssertions;

namespace BrighterTools.FileStorage.Tests;

public class AzureBlobPathBuilderTests
{
    [Fact]
    public void GenerateSafePath_SanitizesSegments()
    {
        var path = AzureBlobPathBuilder.GenerateSafePath(
            "Tenant 1",
            "File 1",
            "My File.pdf",
            1000,
            "Invoices",
            "2026");

        path.Should().Be("tenant_1/invoices/2026/file_1_my_file.pdf");
    }

    [Fact]
    public void ConcatenatePaths_JoinsSanitizedSegments()
    {
        var path = AzureBlobPathBuilder.ConcatenatePaths("Tenant 1", "My Folder");

        path.Should().Be("tenant_1/my_folder");
    }
}
