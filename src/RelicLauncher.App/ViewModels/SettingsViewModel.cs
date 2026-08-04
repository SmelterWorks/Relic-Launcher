using System.Globalization;
using System.Text;
using Avalonia.Threading;
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
    private readonly IRuntimePlatform _platform;
    private readonly IAccountAuthService _accountAuth;
    private readonly IDebugLogBuffer _debugLogBuffer;
    private readonly ILogger<SettingsViewModel> _logger;
    private Action<LauncherSettings>? _onChanged;
    private bool _isBinding;
    private CancellationTokenSource? _saveCts;
    private CancellationTokenSource? _savedIndicatorCts;

    [ObservableProperty]
    private string _installsRoot = string.Empty;

    [ObservableProperty]
    private string _dataPath = string.Empty;

    [ObservableProperty]
    private string _selectedVersion = string.Empty;

    [ObservableProperty]
    private string _accountEmail = string.Empty;

    [ObservableProperty]
    private string _accountPassword = string.Empty;

    [ObservableProperty]
    private string _accountStatus = "Not signed in";

    [ObservableProperty]
    private bool _isSignedIn;

    [ObservableProperty]
    private bool _isSigningIn;

    [ObservableProperty]
    private string _accountError = string.Empty;

    [ObservableProperty]
    private ThemeDefinition? _selectedTheme;

    [ObservableProperty]
    private bool _confirmBeforeExit;

    [ObservableProperty]
    private HomeBackgroundLogoMode _homeBackgroundLogoMode = HomeBackgroundLogoMode.Square;

    [ObservableProperty]
    private string _homeBackgroundCustomLogoPath = string.Empty;

    [ObservableProperty]
    private double _homeBackgroundLogoOpacity = 0.2;

    [ObservableProperty]
    private string _saveStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _debugLogText = string.Empty;

    [ObservableProperty]
    private bool _showDebugViewer;

    public SettingsViewModel(
        ILauncherSettingsStore settingsStore,
        IThemeService themeService,
        IAppPathProvider pathProvider,
        IStoragePickerService storagePicker,
        IRuntimePlatform platform,
        IAccountAuthService accountAuth,
        IFileExplorerService fileExplorer,
        IDebugLogBuffer debugLogBuffer,
        ILogger<SettingsViewModel> logger)
    {
        _settingsStore = settingsStore;
        _themeService = themeService;
        _pathProvider = pathProvider;
        _storagePicker = storagePicker;
        _platform = platform;
        _accountAuth = accountAuth;
        _debugLogBuffer = debugLogBuffer;
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
        _debugLogBuffer.Changed += (_, _) => OnDebugLogChanged();
        RefreshDebugLog();
    }

    public IReadOnlyList<ThemeDefinition> Themes { get; }
    public IReadOnlyList<LogoModeOption> LogoModeOptions { get; }
    public FolderPathRowViewModel LogsFolder { get; }
    public FolderPathRowViewModel ThemesFolder { get; }

    [ObservableProperty]
    private LogoModeOption? _selectedLogoModeOption;

    public bool IsCustomLogoMode => SelectedLogoModeOption?.Mode == HomeBackgroundLogoMode.Custom;

    public bool IsSaveStatusVisible => !string.IsNullOrWhiteSpace(SaveStatusMessage);

    public bool HasAccountError => !string.IsNullOrWhiteSpace(AccountError);

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

    partial void OnInstallsRootChanged(string value) => ScheduleAutoSave();

    partial void OnDataPathChanged(string value) => ScheduleAutoSave();

    partial void OnSelectedThemeChanged(ThemeDefinition? value) => ScheduleAutoSave();

    partial void OnConfirmBeforeExitChanged(bool value) => ScheduleAutoSave();

    partial void OnHomeBackgroundCustomLogoPathChanged(string value) => ScheduleAutoSave();

    partial void OnHomeBackgroundLogoOpacityChanged(double value) => ScheduleAutoSave();

    partial void OnSaveStatusMessageChanged(string value) => OnPropertyChanged(nameof(IsSaveStatusVisible));

    partial void OnAccountErrorChanged(string value) => OnPropertyChanged(nameof(HasAccountError));

    public void Bind(LauncherSettings settings, Action<LauncherSettings> onChanged)
    {
        _isBinding = true;
        _onChanged = onChanged;
        var platform = _platform.GetPlatformInfo();
        InstallsRoot = settings.InstallsRoot ?? platform.DefaultInstallsRoot;
        DataPath = settings.DataPath ?? platform.DefaultDataPath;
        SelectedVersion = settings.SelectedVersion ?? string.Empty;
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
        _ = RefreshAccountStatusAsync();
        RefreshDebugLog();
    }

    [RelayCommand]
    private async Task BrowseInstallsRootAsync()
    {
        var path = await _storagePicker.PickFolderAsync("Select installs root folder").ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
        {
            InstallsRoot = path;
        }
    }

    [RelayCommand]
    private async Task BrowseDataPathAsync()
    {
        var path = await _storagePicker.PickFolderAsync("Select Vintage Story data folder").ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
        {
            DataPath = path;
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

    [RelayCommand]
    private async Task SignInAsync()
    {
        IsSigningIn = true;
        StatusMessage = string.Empty;
        AccountError = string.Empty;
        AccountStatus = "Signing in...";
        try
        {
            var result = await _accountAuth.LoginAsync(new AccountCredentials
            {
                Email = AccountEmail,
                Password = AccountPassword,
            }).ConfigureAwait(true);
            AccountPassword = string.Empty;
            if (!result.IsSuccess)
            {
                var error = result.Error ?? "Sign-in failed.";
                AccountError = error;
                StatusMessage = error;
                IsSignedIn = false;
                AccountStatus = "Not signed in";
                _logger.LogWarning("Settings sign-in failed: {Error}", error);
                return;
            }

            IsSignedIn = true;
            AccountStatus = $"Signed in as {result.Value!.Email}";
            AccountEmail = result.Value.Email ?? AccountEmail;
            AccountError = string.Empty;
            StatusMessage = "Signed in successfully.";
            _logger.LogInformation("Settings sign-in succeeded for {Email}", AccountEmail);
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _accountAuth.LogoutAsync().ConfigureAwait(true);
        IsSignedIn = false;
        AccountStatus = "Not signed in";
        AccountPassword = string.Empty;
        AccountError = string.Empty;
        StatusMessage = "Signed out.";
    }

    [RelayCommand]
    private void RefreshDebugLog()
    {
        var entries = _debugLogBuffer.GetEntries();
        if (entries.Count == 0)
        {
            DebugLogText = "No warnings or errors captured yet.";
            return;
        }

        var sb = new StringBuilder();
        foreach (var entry in entries.Take(120))
        {
            sb.Append(entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.Append(" [").Append(entry.Level).Append("] ");
            if (!string.IsNullOrWhiteSpace(entry.Source))
            {
                sb.Append(entry.Source).Append(": ");
            }

            sb.AppendLine(entry.Message);
            if (!string.IsNullOrWhiteSpace(entry.Exception))
            {
                sb.AppendLine(entry.Exception);
            }

            sb.AppendLine();
        }

        DebugLogText = sb.ToString();
    }

    [RelayCommand]
    private void ClearDebugLog()
    {
        _debugLogBuffer.Clear();
        RefreshDebugLog();
    }

    [RelayCommand]
    private void ToggleDebugViewer() => ShowDebugViewer = !ShowDebugViewer;

    private void OnDebugLogChanged()
    {
        if (!ShowDebugViewer)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            RefreshDebugLog();
        }
        else
        {
            Dispatcher.UIThread.Post(RefreshDebugLog);
        }
    }

    private async Task RefreshAccountStatusAsync()
    {
        var status = await _accountAuth.GetStatusAsync().ConfigureAwait(true);
        if (!status.IsSuccess || status.Value is null || !status.Value.IsSignedIn)
        {
            IsSignedIn = false;
            AccountStatus = "Not signed in";
            return;
        }

        IsSignedIn = true;
        AccountEmail = status.Value.Email ?? AccountEmail;
        AccountStatus = $"Signed in as {status.Value.Email}";
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
            _ = ClearSavedIndicatorAsync(_savedIndicatorCts.Token);
        }
        catch (TaskCanceledException)
        {
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
        }
    }

    private async Task PersistSettingsAsync()
    {
        var platform = _platform.GetPlatformInfo();
        var settings = new LauncherSettings
        {
            InstallsRoot = string.IsNullOrWhiteSpace(InstallsRoot) ? platform.DefaultInstallsRoot : InstallsRoot.Trim(),
            DataPath = string.IsNullOrWhiteSpace(DataPath) ? null : DataPath.Trim(),
            SelectedVersion = string.IsNullOrWhiteSpace(SelectedVersion) ? null : SelectedVersion.Trim(),
            GameInstallPath = string.IsNullOrWhiteSpace(InstallsRoot) || string.IsNullOrWhiteSpace(SelectedVersion)
                ? null
                : Path.Combine(InstallsRoot.Trim(), "versions", SelectedVersion.Trim()),
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
