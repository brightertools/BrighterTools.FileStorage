using BrighterTools.FileStorage.AzureBlob;
using FluentAssertions;

namespace BrighterTools.FileStorage.Tests;

public class AzureBlobValidationTests
{
    [Fact]
    public void ValidateMetadata_AllowsExpectedValues()
    {
        var metadata = new Dictionary<string, string>
        {
            ["uploaded"] = "20260409"
        };

        var action = () => AzureBlobMetadataValidator.ValidateMetadata(metadata);

        action.Should().NotThrow();
    }

    [Fact]
    public void ValidateTags_RejectsInvalidKey()
    {
        var tags = new Dictionary<string, string>
        {
            ["bad key"] = "value"
        };

        var action = () => AzureBlobTagValidator.ValidateTags(tags);

        action.Should().Throw<InvalidOperationException>();
    }
}
