namespace LocalPhotoAI.Shared.Security;

public static class FileValidation
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic", ".heif", ".tiff", ".tif"
    };

    /// <summary>
    /// Returns a sanitized, safe filename derived from the client-supplied name.
    /// Strips path components and replaces dangerous characters.
    /// </summary>
    public static string SanitizeFileName(string clientFileName)
    {
        if (string.IsNullOrWhiteSpace(clientFileName))
            return "unnamed";

        // Strip any path components
        var name = Path.GetFileName(clientFileName);

        // Remove characters that are invalid in filenames
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
            name = name.Replace(c, '_');

        // Collapse multiple underscores
        while (name.Contains("__"))
            name = name.Replace("__", "_");

        name = name.Trim('.', '_', ' ');

        return string.IsNullOrWhiteSpace(name) ? "unnamed" : name;
    }

    /// <summary>
    /// Checks whether the given file extension is in the allowlist.
    /// </summary>
    public static bool IsAllowedExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext);
    }

    /// <summary>
    /// Returns the set of allowed extensions for documentation/error messaging.
    /// </summary>
    public static IReadOnlySet<string> GetAllowedExtensions() => AllowedExtensions;
}
