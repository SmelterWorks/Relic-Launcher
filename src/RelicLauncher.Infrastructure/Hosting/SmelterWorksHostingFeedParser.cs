using System.Text.RegularExpressions;
using System.Xml.Linq;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Infrastructure.Hosting;

internal static partial class SmelterWorksHostingFeedParser
{
    [GeneratedRegex("<[^>]+>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\$(?<amount>\d+)\s*/\s*month", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MonthlyPriceRegex();

    [GeneratedRegex(@"\$(?<amount>\d+)\s*/\s*year", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AnnualPriceRegex();

    internal static IReadOnlyList<HostingPlanInfo> Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml) || xml.TrimStart().StartsWith("<!", StringComparison.Ordinal))
        {
            return [];
        }

        try
        {
            var document = XDocument.Parse(xml);
            var items = document.Descendants().Where(static e => string.Equals(e.Name.LocalName, "item", StringComparison.OrdinalIgnoreCase));
            var plans = new List<HostingPlanInfo>();
            foreach (var item in items)
            {
                var plan = ParseItem(item);
                if (plan is not null)
                {
                    plans.Add(plan);
                }
            }

            return plans;
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static HostingPlanInfo? ParseItem(XElement item)
    {
        var title = ElementValue(item, "title");
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var description = ElementValue(item, "description") ?? string.Empty;
        var plain = HtmlTagRegex().Replace(description, " ").Trim();
        var monthly = TryMatchPrice(plain, MonthlyPriceRegex(), isAnnual: false);
        var annual = TryMatchPrice(plain, AnnualPriceRegex(), isAnnual: true);
        var highlights = plain
            .Split(['\n', '\r', '•', '·'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => line.Length > 2 && !line.StartsWith('$'))
            .Take(6)
            .ToArray();

        return new HostingPlanInfo
        {
            Name = title.Trim(),
            Subtitle = ElementValue(item, "category"),
            MonthlyPrice = monthly,
            AnnualPrice = annual,
            Highlights = highlights.Length > 0 ? highlights : ["See smelterworks.com/hosting for details"],
        };
    }

    private static string? ElementValue(XElement parent, string localName)
        => parent.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? TryMatchPrice(string text, Regex pattern, bool isAnnual)
    {
        var match = pattern.Match(text);
        if (!match.Success)
        {
            return null;
        }

        return isAnnual
            ? $"${match.Groups["amount"].Value} / year"
            : $"${match.Groups["amount"].Value} / month";
    }
}
