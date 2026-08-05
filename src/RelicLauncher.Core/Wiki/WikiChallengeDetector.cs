namespace RelicLauncher.Core.Wiki;

public static class WikiChallengeDetector
{
    public static bool LooksLikeChallenge(string? body, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(contentType)
            && contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
            && !body.Contains("Just a moment", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ContainsAny(
            body,
            "Just a moment",
            "cf-challenge",
            "cf-browser-verification",
            "Attention Required",
            "Enable JavaScript and cookies to continue",
            "cdn-cgi/challenge");
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
