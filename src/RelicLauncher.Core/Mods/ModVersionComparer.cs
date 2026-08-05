using RelicLauncher.Core.Versions;

namespace RelicLauncher.Core.Mods;

public static class ModVersionComparer
{
    public static bool IsAnyVersion(string? version)
        => string.IsNullOrWhiteSpace(version)
           || string.Equals(version.Trim(), "*", StringComparison.Ordinal);

    public static bool Satisfies(string? installed, string? minimum)
    {
        if (IsAnyVersion(minimum))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(installed))
        {
            return false;
        }

        return Compare(installed, minimum) >= 0;
    }

    public static int Compare(string? left, string? right)
        => GameVersionComparer.Compare(left, right);

    public static string? TakeHigherMinimum(string? left, string? right)
    {
        if (IsAnyVersion(left))
        {
            return IsAnyVersion(right) ? null : right?.Trim();
        }

        if (IsAnyVersion(right))
        {
            return left?.Trim();
        }

        return Compare(left, right) >= 0 ? left!.Trim() : right!.Trim();
    }
}
