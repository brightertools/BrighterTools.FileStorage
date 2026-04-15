using System.Text.RegularExpressions;

namespace BrighterTools.FileStorage.AzureBlob;

/// <summary>
/// Represents Azure Blob Path Builder.
/// </summary>
public static class AzureBlobPathBuilder
{
    private static readonly Regex SanitizeRegex = new(@"[^a-zA-Z0-9\-_\.]", RegexOptions.Compiled);

    /// <summary>
    /// Executes Concatenate Paths.
    /// </summary>
    public static string ConcatenatePaths(params string[] paths)
    {
        var sanitizedPaths = paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => SanitizePathSegment(p.Trim().ToLowerInvariant()))
            .ToList();

        return string.Join("/", sanitizedPaths);
    }

    /// <summary>
    /// Executes Sanitize Path Segment.
    /// </summary>
    public static string SanitizePathSegment(string originalName)
    {
        if (string.IsNullOrWhiteSpace(originalName))
        {
            return string.Empty;
        }

        var sanitized = SanitizeRegex.Replace(originalName.Trim(), "_").Trim('_').ToLowerInvariant();
        return sanitized;
    }

    /// <summary>
    /// Generates Safe Path.
    /// </summary>
    public static string GenerateSafePath(string tenantIdentifier, string fileIdentifier, string originalFilename, int maxPathLength = 1000, params string[] paths)
    {
        if (string.IsNullOrWhiteSpace(originalFilename))
        {
            throw new ArgumentException("Filename cannot be null or empty.", nameof(originalFilename));
        }

        if (string.IsNullOrWhiteSpace(fileIdentifier))
        {
            throw new ArgumentException("File identifier cannot be null or empty.", nameof(fileIdentifier));
        }

        if (string.IsNullOrWhiteSpace(tenantIdentifier))
        {
            throw new ArgumentException("Tenant identifier cannot be null or empty.", nameof(tenantIdentifier));
        }

        var tenant = SanitizePathSegment(tenantIdentifier);
        var file = SanitizePathSegment(fileIdentifier);
        var ext = Path.GetExtension(originalFilename);
        var baseName = Path.GetFileNameWithoutExtension(originalFilename);
        var sanitizedBaseName = SanitizePathSegment(baseName);
        var sanitizedExt = SanitizePathSegment(ext);
        var filenameWithIdentifier = $"{file}_{sanitizedBaseName}{sanitizedExt}";
        var fullPathSegments = new List<string> { tenant };

        if (paths is { Length: > 0 })
        {
            fullPathSegments.AddRange(paths.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => SanitizePathSegment(p.Trim().ToLowerInvariant())));
        }

        var staticPrefix = string.Join("/", fullPathSegments) + "/";
        var maxFilenameLength = maxPathLength - staticPrefix.Length;

        if (filenameWithIdentifier.Length > maxFilenameLength)
        {
            var baseNameMax = Math.Max(0, maxFilenameLength - file.Length - sanitizedExt.Length - 1);
            sanitizedBaseName = sanitizedBaseName.Substring(0, Math.Min(sanitizedBaseName.Length, baseNameMax));
            filenameWithIdentifier = $"{file}_{sanitizedBaseName}{sanitizedExt}";
        }

        fullPathSegments.Add(filenameWithIdentifier);
        return string.Join("/", fullPathSegments);
    }
}

