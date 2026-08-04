using System.Reflection;

namespace RelicLauncher.Core;

public static class BuildMetadata
{
    public static string Version
    {
        get
        {
            var version = typeof(BuildMetadata).Assembly.GetName().Version;
            return version is null ? "0.1.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static string CommitSha => GetMetadata("CommitSha") ?? "unknown";

    public static string BuildTimeUtc => GetMetadata("BuildTimeUtc") ?? "unknown";

    private static string? GetMetadata(string key)
        => typeof(BuildMetadata).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            ?.Value;
}
