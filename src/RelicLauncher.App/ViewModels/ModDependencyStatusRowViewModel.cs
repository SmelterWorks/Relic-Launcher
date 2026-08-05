using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public sealed class ModDependencyStatusRowViewModel
{
    public ModDependencyStatusRowViewModel(ModDependencyIssue issue)
    {
        ModId = issue.RequiredModId;
        MinimumVersion = issue.RequiredMinimumVersion ?? "*";
        Kind = issue.Kind;
        InstalledVersion = issue.InstalledVersion;
        Label = BuildLabel(issue);
    }

    public ModDependencyStatusRowViewModel(ModDependencyRequirement requirement, ModDependencyIssueKind kind, string? installedVersion = null)
    {
        ModId = requirement.ModId;
        MinimumVersion = requirement.AllowsAnyVersion ? "*" : (requirement.MinimumVersion ?? "*");
        Kind = kind;
        InstalledVersion = installedVersion;
        Label = BuildLabel(ModId, MinimumVersion, kind, installedVersion);
    }

    public string ModId { get; }
    public string MinimumVersion { get; }
    public ModDependencyIssueKind Kind { get; }
    public string? InstalledVersion { get; }
    public string Label { get; }
    public bool IsBlocking => Kind is not ModDependencyIssueKind.Satisfied;

    private static string BuildLabel(ModDependencyIssue issue)
        => BuildLabel(issue.RequiredModId, issue.RequiredMinimumVersion ?? "*", issue.Kind, issue.InstalledVersion);

    private static string BuildLabel(string modId, string minimum, ModDependencyIssueKind kind, string? installed)
    {
        var versionPart = string.IsNullOrWhiteSpace(minimum) || string.Equals(minimum, "*", StringComparison.Ordinal)
            ? modId
            : $"{modId} >= {minimum}";
        return kind switch
        {
            ModDependencyIssueKind.Satisfied => string.IsNullOrWhiteSpace(installed)
                ? $"{versionPart}: ok"
                : $"{versionPart}: ok ({installed})",
            ModDependencyIssueKind.Missing => $"{versionPart}: missing",
            ModDependencyIssueKind.Disabled => $"{versionPart}: installed but disabled",
            ModDependencyIssueKind.Outdated => $"{versionPart}: outdated ({installed ?? "?"})",
            ModDependencyIssueKind.BuiltinVersionMismatch => $"{versionPart}: game version too old ({installed ?? "?"})",
            ModDependencyIssueKind.Cycle => $"{versionPart}: cycle",
            ModDependencyIssueKind.Unresolved => $"{versionPart}: unresolved",
            _ => versionPart,
        };
    }
}
