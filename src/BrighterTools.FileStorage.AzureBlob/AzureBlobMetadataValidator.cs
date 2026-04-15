using System.Text;
using System.Text.RegularExpressions;

namespace BrighterTools.FileStorage.AzureBlob;

/// <summary>
/// Represents Azure Blob Metadata Validator.
/// </summary>
public static class AzureBlobMetadataValidator
{
    private static readonly Regex StrictMetadataKeyRegex = new(@"^[a-zA-Z][a-zA-Z0-9\-]{0,127}$", RegexOptions.Compiled);

    /// <summary>
    /// Validates Metadata Key.
    /// </summary>
    public static bool ValidateMetadataKey(string key) => !string.IsNullOrWhiteSpace(key) && StrictMetadataKeyRegex.IsMatch(key);

    /// <summary>
    /// Validates Metadata Value.
    /// </summary>
    public static bool ValidateMetadataValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (Encoding.UTF8.GetBytes(value).Length > 2048)
        {
            return false;
        }

        return value.All(c => !char.IsControl(c));
    }

    /// <summary>
    /// Validates Metadata.
    /// </summary>
    public static void ValidateMetadata(IDictionary<string, string> metadata)
    {
        var invalidKeys = metadata.Where(kvp => !ValidateMetadataKey(kvp.Key)).Select(kvp => kvp.Key).ToList();
        var invalidValues = metadata.Where(kvp => !ValidateMetadataValue(kvp.Value)).Select(kvp => kvp.Key).ToList();

        if (!invalidKeys.Any() && !invalidValues.Any())
        {
            return;
        }

        var message = new StringBuilder("Azure blob metadata validation failed.");

        if (invalidKeys.Any())
        {
            message.AppendLine($" Invalid keys: {string.Join(", ", invalidKeys)}.");
        }

        if (invalidValues.Any())
        {
            message.AppendLine($" Invalid values (by key): {string.Join(", ", invalidValues)}.");
        }

        throw new InvalidOperationException(message.ToString());
    }
}

