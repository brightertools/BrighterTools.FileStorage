using BrighterTools.FileStorage;
using FluentAssertions;

namespace BrighterTools.FileStorage.Tests;

public class StorageTreeBuilderTests
{
    [Fact]
    public void BuildTree_CreatesNestedNodes()
    {
        var tree = StorageTreeBuilder.BuildTree(
            [
                "tenant/images/file-one.png",
                "tenant/images/file-two.png",
                "tenant/docs/readme.pdf"
            ],
            "tenant");

        tree.Name.Should().Be("tenant");
        tree.Children.Should().ContainSingle(x => x.Name == "images");
        tree.Children.Should().ContainSingle(x => x.Name == "docs");
    }
}
