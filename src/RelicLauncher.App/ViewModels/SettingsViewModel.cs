using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class SettingsViewModel : PageViewModelBase
{
    private readonly ILauncherSettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly IAppPathProvider _pathProvider;
    private readonly IStoragePickerService _storagePicker;
    private readonly ILogger<SettingsViewModel> _logger;
    private Action<LauncherSettings>? _onChanged;
    private bool _isBinding;
    private CancellationTokenSource? _saveCts;
    private CancellationTokenSource? _savedIndicatorCts;

    [ObservableProperty]
    private string _gameInstallPath = string.Empty;

    [ObservableProperty]
    private ThemeDefinition? _selectedTheme;

    [ObservableProperty]
    private bool _confirmBeforeExit;

    [ObservableProperty]
    private HomeBackgroundLogoMode _homeBackgroundLogoMode = HomeBackgroundLogoMode.Square;

    [ObservableProperty]
    private string _homeBackgroundCustomLogoPath = string.Empty;

    [ObservableProperty]
    private double _homeBackgroundLogoOpacity = 0.12;

    [ObservableProperty]
    private string _saveStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    public SettingsViewModel(
        ILauncherSettingsStore settingsStore,
        IThemeService themeService,
        IAppPathProvider pathProvider,
        IStoragePickerService storagePicker,
        IFileExplorerService fileExplorer,
        ILogger<SettingsViewModel> logger)
    {
        _settingsStore = settingsStore;
        _themeService = themeService;
        _pathProvider = pathProvider;
        _storagePicker = storagePicker;
        _logger = logger;
        Themes = _themeService.AvailableThemes;
        LogoModeOptions =
        [
            new LogoModeOption { Mode = HomeBackgroundLogoMode.None, Label = "None" },
            new LogoModeOption { Mode = HomeBackgroundLogoMode.Square, Label = "Vintage Story square" },
            new LogoModeOption { Mode = HomeBackgroundLogoMode.Banner, Label = "Vintage Story banner" },
            new LogoModeOption { Mode = HomeBackgroundLogoMode.Custom, Label = "Custom image file" },
        ];
        LogsFolder = new FolderPathRowViewModel(fileExplorer);
        ThemesFolder = new FolderPathRowViewModel(fileExplorer);
    }

    public IReadOnlyList<ThemeDefinition> Themes { get; }
    public IReadOnlyList<LogoModeOption> LogoModeOptions { get; }
    public FolderPathRowViewModel LogsFolder { get; }
    public FolderPathRowViewModel ThemesFolder { get; }

    [ObservableProperty]
    private LogoModeOption? _selectedLogoModeOption;

    public bool IsCustomLogoMode => SelectedLogoModeOption?.Mode == HomeBackgroundLogoMode.Custom;

    public bool IsSaveStatusVisible => !string.IsNullOrWhiteSpace(SaveStatusMessage);

    partial void OnSelectedLogoModeOptionChanged(LogoModeOption? value)
    {
        if (value is not null)
        {
            HomeBackgroundLogoMode = value.Mode;
        }

        OnPropertyChanged(nameof(IsCustomLogoMode));
        ScheduleAutoSave();
    }

    partial void OnHomeBackgroundLogoModeChanged(HomeBackgroundLogoMode value)
        => OnPropertyChanged(nameof(IsCustomLogoMode));

    partial void OnGameInstallPathChanged(string value) => ScheduleAutoSave();

    partial void OnSelectedThemeChanged(ThemeDefinition? value) => ScheduleAutoSave();

    partial void OnConfirmBeforeExitChanged(bool value) => ScheduleAutoSave();

    partial void OnHomeBackgroundCustomLogoPathChanged(string value) => ScheduleAutoSave();

    partial void OnHomeBackgroundLogoOpacityChanged(double value) => ScheduleAutoSave();

    partial void OnSaveStatusMessageChanged(string value) => OnPropertyChanged(nameof(IsSaveStatusVisible));

    public void Bind(LauncherSettings settings, Action<LauncherSettings> onChanged)
    {
        _isBinding = true;
        _onChanged = onChanged;
        GameInstallPath = settings.GameInstallPath ?? string.Empty;
        ConfirmBeforeExit = settings.ConfirmBeforeExit;
        HomeBackgroundLogoMode = settings.HomeBackgroundLogoMode;
        SelectedLogoModeOption = LogoModeOptions.FirstOrDefault(o => o.Mode == settings.HomeBackgroundLogoMode)
            ?? LogoModeOptions.FirstOrDefault(o => o.Mode == HomeBackgroundLogoMode.Square);
        HomeBackgroundCustomLogoPath = settings.HomeBackgroundCustomLogoPath ?? string.Empty;
        HomeBackgroundLogoOpacity = settings.HomeBackgroundLogoOpacity;
        SelectedTheme = Themes.FirstOrDefault(t => string.Equals(t.Id, settings.SelectedThemeId, StringComparison.OrdinalIgnoreCase))
            ?? Themes.FirstOrDefault();
        var paths = _pathProvider.GetPaths();
        LogsFolder.Bind("Logs folder", paths.LogsDirectory);
        ThemesFolder.Bind("User themes folder", paths.ThemesDirectory);
        SaveStatusMessage = string.Empty;
        StatusMessage = string.Empty;
        _isBinding = false;
    }

    [RelayCommand]
    private async Task BrowseGameInstallPathAsync()
    {
        var path = await _storagePicker.PickFolderAsync("Select Vintage Story install folder").ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
        {
            GameInstallPath = path;
        }
    }

    [RelayCommand]
    private async Task BrowseCustomLogoPathAsync()
    {
        var path = await _storagePicker.PickImageFileAsync("Select home background logo").ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
        {
            HomeBackgroundCustomLogoPath = path;
        }
    }

    private void ScheduleAutoSave()
    {
        if (_isBinding)
        {
            return;
        }

        _saveCts?.Cancel();
        _saveCts = new CancellationTokenSource();
        var token = _saveCts.Token;
        _ = AutoSaveAsync(token);
    }

    private async Task AutoSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsSaving = true;
            SaveStatusMessage = "Saving...";
            await Task.Delay(450, cancellationToken).ConfigureAwait(true);
            await PersistSettingsAsync().ConfigureAwait(true);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            SaveStatusMessage = "Saved";
            _savedIndicatorCts?.Cancel();
            _savedIndicatorCts = new CancellationTokenSource();
            var indicatorToken = _savedIndicatorCts.Token;
            _ = ClearSavedIndicatorAsync(indicatorToken);
        }
        catch (TaskCanceledException)
        {
            // Debounced save superseded by a newer edit.
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsSaving = false;
            }
        }
    }

    private async Task ClearSavedIndicatorAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(2000, cancellationToken).ConfigureAwait(true);
            if (!cancellationToken.IsCancellationRequested && string.Equals(SaveStatusMessage, "Saved", StringComparison.Ordinal))
            {
                SaveStatusMessage = string.Empty;
            }
        }
        catch (TaskCanceledException)
        {
            // A new save cycle started.
        }
    }

    private async Task PersistSettingsAsync()
    {
        var settings = new LauncherSettings
        {
            GameInstallPath = string.IsNullOrWhiteSpace(GameInstallPath) ? null : GameInstallPath.Trim(),
            SelectedThemeId = SelectedTheme?.Id ?? LauncherSettings.DefaultThemeId,
            ConfirmBeforeExit = ConfirmBeforeExit,
            HomeBackgroundLogoMode = SelectedLogoModeOption?.Mode ?? HomeBackgroundLogoMode.Square,
            HomeBackgroundCustomLogoPath = string.IsNullOrWhiteSpace(HomeBackgroundCustomLogoPath)
                ? null
                : HomeBackgroundCustomLogoPath.Trim(),
            HomeBackgroundLogoOpacity = Math.Clamp(HomeBackgroundLogoOpacity, 0.02, 1.0),
        };

        var themeResult = _themeService.ApplyTheme(settings.SelectedThemeId);
        if (!themeResult.IsSuccess)
        {
            SaveStatusMessage = themeResult.Error ?? "Theme apply failed.";
            return;
        }

        var save = await _settingsStore.SaveAsync(settings).ConfigureAwait(true);
        if (!save.IsSuccess)
        {
            _logger.LogWarning("Settings save failed: {Error}", save.Error);
            SaveStatusMessage = save.Error ?? "Save failed.";
            return;
        }

        _onChanged?.Invoke(settings);
    }
}
