namespace RelicLauncher.Core.Paths;

public static class PathValidator
{
    public static bool TryGetFullPath(string? path, out string fullPath, out string error)
    {
        fullPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Path is empty.";
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"Invalid path: {ex.Message}";
            return false;
        }
    }

    public static bool TryResolveChildPath(string rootDir, string relativeEntryPath, out string destination)
    {
        destination = string.Empty;
        if (string.IsNullOrWhiteSpace(relativeEntryPath))
        {
            return false;
        }

        var normalizedRoot = Path.GetFullPath(rootDir);
        var candidate = Path.GetFullPath(Path.Combine(normalizedRoot, relativeEntryPath));
        var withSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(withSeparator, StringComparison.Ordinal))
        {
            return false;
        }

        destination = candidate;
        return true;
    }
}
