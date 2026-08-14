using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public sealed class HostingPlanCardViewModel
{
    public HostingPlanCardViewModel(HostingPlanInfo plan)
    {
        Name = plan.Name;
        Subtitle = plan.Subtitle ?? string.Empty;
        MonthlyPrice = plan.MonthlyPrice ?? string.Empty;
        AnnualPrice = plan.AnnualPrice ?? string.Empty;
        Highlights = plan.Highlights;
        HasMonthlyPrice = !string.IsNullOrWhiteSpace(MonthlyPrice);
        HasAnnualPrice = !string.IsNullOrWhiteSpace(AnnualPrice);
        HasSubtitle = !string.IsNullOrWhiteSpace(Subtitle);
        PriceSummary = BuildPriceSummary(MonthlyPrice, AnnualPrice, HasMonthlyPrice, HasAnnualPrice);
    }

    public string Name { get; }

    public string Subtitle { get; }

    public string MonthlyPrice { get; }

    public string AnnualPrice { get; }

    public string PriceSummary { get; }

    public IReadOnlyList<string> Highlights { get; }

    public bool HasMonthlyPrice { get; }

    public bool HasAnnualPrice { get; }

    public bool HasSubtitle { get; }

    public string DisplayTitle => HasSubtitle
        ? Subtitle
        : IsCodeName(Name)
            ? Name
            : ExtractLabel(Name);

    public bool ShowCodeName => IsCodeName(Name) && HasSubtitle;

    public string CodeName => Name;

    private static string BuildPriceSummary(string monthly, string annual, bool hasMonthly, bool hasAnnual)
    {
        if (hasMonthly && hasAnnual)
        {
            return $"{monthly}  |  {annual}";
        }

        if (hasMonthly)
        {
            return monthly;
        }

        return hasAnnual ? annual : string.Empty;
    }

    private static bool IsCodeName(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && !value.Contains(':')
        && !value.Contains('$')
        && value.Length <= 32;

    private static string ExtractLabel(string value)
    {
        var colon = value.IndexOf(':');
        return colon > 0 ? value[..colon].Trim() : value.Trim();
    }
}
