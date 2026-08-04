using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.App.Services;

public sealed class AvaloniaThemeService : IThemeService
{
    private readonly IThemeCatalog _catalog;
    private readonly ILogger<AvaloniaThemeService> _logger;
    private ResourceDictionary? _currentThemeDictionary;

    public AvaloniaThemeService(IThemeCatalog catalog, ILogger<AvaloniaThemeService> logger)
    {
        _catalog = catalog;
        _logger = logger;
        CurrentThemeId = LauncherSettings.DefaultThemeId;
    }

    public string CurrentThemeId { get; private set; }

    public IReadOnlyList<ThemeDefinition> AvailableThemes => _catalog.GetThemes();

    public Result ApplyTheme(string themeId)
    {
        var theme = _catalog.FindById(themeId) ?? _catalog.FindById(LauncherSettings.DefaultThemeId);
        if (theme is null || string.IsNullOrWhiteSpace(theme.ResourceUri))
        {
            return Result.Failure($"Theme not found: {themeId}");
        }

        try
        {
            var app = Application.Current;
            if (app is null)
            {
                return Result.Failure("Application.Current is null.");
            }

            var loaded = AvaloniaXamlLoader.Load(new Uri(theme.ResourceUri));
            if (loaded is not ResourceDictionary dictionary)
            {
                return Result.Failure($"Theme resource is not a ResourceDictionary: {theme.ResourceUri}");
            }

            if (_currentThemeDictionary is not null)
            {
                app.Resources.MergedDictionaries.Remove(_currentThemeDictionary);
            }

            app.Resources.MergedDictionaries.Add(dictionary);
            _currentThemeDictionary = dictionary;
            CurrentThemeId = theme.Id;
            app.RequestedThemeVariant = ThemeVariant.Dark;

            _logger.LogInformation("Applied theme {ThemeId}", theme.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply theme {ThemeId}", themeId);
            return Result.Failure(ex.Message);
        }
    }
}
