namespace RelicLauncher.Core.Security;

public static partial class ServerDisplaySanitizer
{
    public const int MaxNameLength = 120;
    public const int MaxDescriptionLength = 2000;

    public static string SanitizeName(string? value)
        => Truncate(StripUnsafe(SanitizePlainText(value)), MaxNameLength);

    public static string SanitizeDescription(string? value)
        => Truncate(StripUnsafe(SanitizePlainText(value)), MaxDescriptionLength);

    private static string SanitizePlainText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var stripped = StripTags(value);
        return stripped.Trim();
    }

    private static string StripTags(string value)
    {
        var result = new System.Text.StringBuilder(value.Length);
        var insideTag = false;
        foreach (var ch in value)
        {
            if (ch == '<')
            {
                insideTag = true;
                continue;
            }

            if (ch == '>')
            {
                insideTag = false;
                continue;
            }

            if (!insideTag)
            {
                result.Append(ch);
            }
        }

        return result.ToString();
    }

    private static string StripUnsafe(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch is '\uFEFF' or '\u202E' or '\u202D' or '\u2066' or '\u2067' or '\u2068' or '\u2069')
            {
                continue;
            }

            if (char.IsControl(ch) && ch is not '\n' and not '\r' and not '\t')
            {
                continue;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength - 1), "…");
    }
}
