using RelicLauncher.Core.Models;

namespace RelicLauncher.Core.Abstractions;

public interface IThemeCatalog
{
    IReadOnlyList<ThemeDefinition> GetThemes();
    ThemeDefinition? FindById(string themeId);
}
