namespace AtomUI.City.Templates;

/// <summary>
/// Represents template change.
/// </summary>
public sealed record TemplateChange(string Type, string Path)
{
    /// <summary>
    /// Executes the create operation.
    /// </summary>
    public static TemplateChange Create(string path)
    {
        return new TemplateChange("create", NormalizePath(path));
    }

    internal static string NormalizePath(string path)
    {
        return TryNormalizePath(path, out var normalizedPath, out var error)
            ? normalizedPath
            : throw new ArgumentException(error, nameof(path));
    }

    internal static bool TryNormalizePath(string path, out string normalizedPath, out string error)
    {
        normalizedPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Template path cannot be empty.";
            return false;
        }

        var normalizedSeparators = path.Replace('\\', '/');
        if (normalizedSeparators.StartsWith("/", StringComparison.Ordinal) ||
            normalizedSeparators.StartsWith("//", StringComparison.Ordinal) ||
            IsWindowsRootedPath(normalizedSeparators))
        {
            error = "Template path must be relative.";
            return false;
        }

        var segments = new List<string>();
        foreach (var segment in normalizedSeparators.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                error = "Template path cannot escape the package root.";
                return false;
            }

            if (segment.Contains('\0', StringComparison.Ordinal))
            {
                error = "Template path cannot contain null characters.";
                return false;
            }

            if (segment.Any(static character =>
                    char.IsControl(character) || character is '<' or '>' or ':' or '"' or '|' or '?' or '*') ||
                segment.EndsWith(' ') ||
                segment.EndsWith('.') ||
                IsReservedWindowsName(segment))
            {
                error = "Template path contains a non-portable segment.";
                return false;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            error = "Template path cannot be empty.";
            return false;
        }

        normalizedPath = string.Join('/', segments);
        return true;
    }

    private static bool IsWindowsRootedPath(string path)
    {
        return path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':';
    }

    private static bool IsReservedWindowsName(string segment)
    {
        var stem = segment.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return stem.Length == 4 &&
            (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
            stem[3] is >= '1' and <= '9';
    }
}
