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
    public const string FrostSpireId = "frost-spire";
    public const string ObsidianForgeId = "obsidian-forge";
    public const string AmberDawnId = "amber-dawn";
    public const string DriftersLanternId = "drifters-lantern";

    public const string RelicDefaultUri = "avares://RelicLauncher.Themes/Themes/RelicDefault.axaml";
    public const string HighContrastUri = "avares://RelicLauncher.Themes/Themes/HighContrast.axaml";
    public const string TemporalRiftUri = "avares://RelicLauncher.Themes/Themes/TemporalRift.axaml";
    public const string MossHearthUri = "avares://RelicLauncher.Themes/Themes/MossHearth.axaml";
    public const string CopperDungeonUri = "avares://RelicLauncher.Themes/Themes/CopperDungeon.axaml";
    public const string FrostSpireUri = "avares://RelicLauncher.Themes/Themes/FrostSpire.axaml";
    public const string ObsidianForgeUri = "avares://RelicLauncher.Themes/Themes/ObsidianForge.axaml";
    public const string AmberDawnUri = "avares://RelicLauncher.Themes/Themes/AmberDawn.axaml";
    public const string DriftersLanternUri = "avares://RelicLauncher.Themes/Themes/DriftersLantern.axaml";

    private readonly IReadOnlyList<ThemeDefinition> _themes =
    [
        Theme(RelicDefaultId, "Relic (Vintage Story)", RelicDefaultUri, "#FF1A1510", "#FFB87333"),
        Theme(TemporalRiftId, "Temporal Rift", TemporalRiftUri, "#FF12171C", "#FF6FA8C4"),
        Theme(MossHearthId, "Moss Hearth", MossHearthUri, "#FF0F1612", "#FF8FAE5A"),
        Theme(CopperDungeonId, "Copper Dungeon", CopperDungeonUri, "#FF141210", "#FFC48A5A"),
        Theme(FrostSpireId, "Frost Spire", FrostSpireUri, "#FF0C1218", "#FF7EB8D8"),
        Theme(ObsidianForgeId, "Obsidian Forge", ObsidianForgeUri, "#FF100A0A", "#FFD06048"),
        Theme(AmberDawnId, "Amber Dawn", AmberDawnUri, "#FF1A140E", "#FFE8A848"),
        Theme(DriftersLanternId, "Drifter's Lantern", DriftersLanternUri, "#FF0E1214", "#FFE8B848"),
        Theme(HighContrastId, "High Contrast", HighContrastUri, "#FF000000", "#FFFFFF00"),
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

    private static ThemeDefinition Theme(
        string id,
        string displayName,
        string resourceUri,
        string previewBackgroundHex,
        string previewAccentHex)
        => new()
        {
            Id = id,
            DisplayName = displayName,
            IsBuiltIn = true,
            ResourceUri = resourceUri,
            PreviewBackgroundHex = previewBackgroundHex,
            PreviewAccentHex = previewAccentHex,
        };
}
