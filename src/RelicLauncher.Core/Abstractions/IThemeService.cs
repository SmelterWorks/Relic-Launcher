using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IThemeService
{
    string CurrentThemeId { get; }
    IReadOnlyList<ThemeDefinition> AvailableThemes { get; }
    Result ApplyTheme(string themeId);
}
