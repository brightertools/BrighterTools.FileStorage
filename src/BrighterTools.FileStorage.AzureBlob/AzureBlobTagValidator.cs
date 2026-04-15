using System.Text;
using System.Text.RegularExpressions;

namespace BrighterTools.FileStorage.AzureBlob;

/// <summary>
/// Represents Azure Blob Tag Validator.
/// </summary>
public static class AzureBlobTagValidator
{
    private static readonly Regex ValidTagKeyRegex = new(@"^[a-zA-Z0-9_-]{1,128}$", RegexOptions.Compiled);

    /// <summary>
    /// Validates Tag Key.
    /// </summary>
    public static bool ValidateTagKey(string key) => !string.IsNullOrWhiteSpace(key) && ValidTagKeyRegex.IsMatch(key);

    /// <summary>
    /// Validates Tag Value.
    /// </summary>
    public static bool ValidateTagValue(string value)
    {
        if (value == null)
        {
            return false;
        }

        if (Encoding.UTF8.GetByteCount(value) > 256)
        {
            return false;
        }

        return value.All(c => !char.IsControl(c));
    }

    /// <summary>
    /// Validates Tags.
    /// </summary>
    public static void ValidateTags(IDictionary<string, string> tags)
    {
        var invalidKeys = tags.Where(kvp => !ValidateTagKey(kvp.Key)).Select(kvp => kvp.Key).ToList();
        var invalidValues = tags.Where(kvp => !ValidateTagValue(kvp.Value)).Select(kvp => kvp.Key).ToList();

        if (!invalidKeys.Any() && !invalidValues.Any())
        {
            return;
        }

        var message = new StringBuilder("Azure blob tag validation failed.");

        if (invalidKeys.Any())
        {
            message.AppendLine($" Invalid tag keys: {string.Join(", ", invalidKeys)}.");
        }

        if (invalidValues.Any())
        {
            message.AppendLine($" Invalid tag values (by key): {string.Join(", ", invalidValues)}.");
        }

        throw new InvalidOperationException(message.ToString());
    }
}

