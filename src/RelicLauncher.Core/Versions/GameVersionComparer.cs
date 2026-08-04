using System.Globalization;

namespace RelicLauncher.Core.Versions;

public static class GameVersionComparer
{
    public static int Compare(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(left))
        {
            return -1;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return 1;
        }

        var leftParts = ParseParts(left);
        var rightParts = ParseParts(right);
        var count = Math.Max(leftParts.Count, rightParts.Count);
        for (var i = 0; i < count; i++)
        {
            var l = i < leftParts.Count ? leftParts[i] : 0;
            var r = i < rightParts.Count ? rightParts[i] : 0;
            var cmp = l.CompareTo(r);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static List<int> ParseParts(string version)
    {
        var parts = new List<int>();
        var buffer = string.Empty;
        foreach (var ch in version)
        {
            if (char.IsDigit(ch))
            {
                buffer += ch;
                continue;
            }

            if (buffer.Length > 0)
            {
                parts.Add(int.Parse(buffer, CultureInfo.InvariantCulture));
                buffer = string.Empty;
            }
        }

        if (buffer.Length > 0)
        {
            parts.Add(int.Parse(buffer, CultureInfo.InvariantCulture));
        }

        return parts;
    }
}
