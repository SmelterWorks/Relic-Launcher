using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Themes;

public sealed class BuiltInThemeCatalog : IThemeCatalog
{
    public const string RelicDefaultId = "relic-default";
    public const string HighContrastId = "high-contrast";
    public const string TemporalRiftId = "temporal-rift";
    public const string MossHearthId = "moss-hearth";
    public const string CopperDungeonId = "copper-dungeon";

    public const string RelicDefaultUri = "avares://RelicLauncher.Themes/Themes/RelicDefault.axaml";
    public const string HighContrastUri = "avares://RelicLauncher.Themes/Themes/HighContrast.axaml";
    public const string TemporalRiftUri = "avares://RelicLauncher.Themes/Themes/TemporalRift.axaml";
    public const string MossHearthUri = "avares://RelicLauncher.Themes/Themes/MossHearth.axaml";
    public const string CopperDungeonUri = "avares://RelicLauncher.Themes/Themes/CopperDungeon.axaml";

    private readonly IReadOnlyList<ThemeDefinition> _themes =
    [
        new ThemeDefinition
        {
            Id = RelicDefaultId,
            DisplayName = "Relic (Vintage Story)",
            IsBuiltIn = true,
            ResourceUri = RelicDefaultUri,
        },
        new ThemeDefinition
        {
            Id = TemporalRiftId,
            DisplayName = "Temporal Rift",
            IsBuiltIn = true,
            ResourceUri = TemporalRiftUri,
        },
        new ThemeDefinition
        {
            Id = MossHearthId,
            DisplayName = "Moss Hearth",
            IsBuiltIn = true,
            ResourceUri = MossHearthUri,
        },
        new ThemeDefinition
        {
            Id = CopperDungeonId,
            DisplayName = "Copper Dungeon",
            IsBuiltIn = true,
            ResourceUri = CopperDungeonUri,
        },
        new ThemeDefinition
        {
            Id = HighContrastId,
            DisplayName = "High Contrast",
            IsBuiltIn = true,
            ResourceUri = HighContrastUri,
        },
    ];

    public IReadOnlyList<ThemeDefinition> GetThemes() => _themes;

    public ThemeDefinition? FindById(string themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId))
        {
            return null;
        }

        return _themes.FirstOrDefault(t => string.Equals(t.Id, themeId, StringComparison.OrdinalIgnoreCase));
    }
}
