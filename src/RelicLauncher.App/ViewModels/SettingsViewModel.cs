using System.Globalization;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
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
    private readonly IClientSettingsSessionWriter _sessionWriter;
    private readonly IDebugLogBuffer _debugLogBuffer;
    private readonly IConfirmDialogService _confirmDialog;
    private readonly ISandboxSupport _sandboxSupport;
    private readonly ILogger<SettingsViewModel> _logger;
    private Action<LauncherSettings>? _onChanged;
    private bool _isBinding;
    private List<string> _modUpdateOptOutModIds = [];
    private CancellationTokenSource? _saveCts;
    private CancellationTokenSource? _savedIndicatorCts;
    private int _saveGeneration;
    private string? _preLoginToken;

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
    private string _accountTotpCode = string.Empty;

    [ObservableProperty]
    private bool _requiresTotp;

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
    private bool _warnOnBlockedMods = true;

    [ObservableProperty]
    private ModUpdateMode _modUpdateMode = ModUpdateMode.Prompt;

    [ObservableProperty]
    private ModUpdateModeOption? _selectedModUpdateModeOption;

    [ObservableProperty]
    private HomeBackgroundLogoMode _homeBackgroundLogoMode = HomeBackgroundLogoMode.Square;

    [ObservableProperty]
    private string _homeBackgroundCustomLogoPath = string.Empty;

    [ObservableProperty]
    private double _homeBackgroundLogoOpacity = RelicDefaults.HomeBackgroundLogoOpacity;

    [ObservableProperty]
    private string _accountBaseUrl = VintageStoryEndpoints.AccountBaseUrl;

    [ObservableProperty]
    private string _cdnBaseUrl = VintageStoryEndpoints.CdnBaseUrl;

    [ObservableProperty]
    private string _modDbApiBaseUrl = VintageStoryEndpoints.ModDbApiBaseUrl;

    [ObservableProperty]
    private string _modDbDownloadBaseUrl = VintageStoryEndpoints.ModDbDownloadBaseUrl;

    [ObservableProperty]
    private string _versionCatalogUrl = VintageStoryEndpoints.VersionCatalogUrl;

    [ObservableProperty]
    private string _latestStableUrl = VintageStoryEndpoints.LatestStableUrl;

    [ObservableProperty]
    private string _newsBlogUrl = VintageStoryEndpoints.NewsBlogUrl;

    [ObservableProperty]
    private string _wikiBaseUrl = VintageStoryEndpoints.WikiBaseUrl;

    [ObservableProperty]
    private string _serverListUrl = RelicLauncherEndpoints.ServerListUrl;

    [ObservableProperty]
    private string _saveStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _saveStatusIsError;

    [ObservableProperty]
    private bool _isAccountSectionExpanded = true;

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
        IClientSettingsSessionWriter sessionWriter,
        IFileExplorerService fileExplorer,
        IDebugLogBuffer debugLogBuffer,
        IConfirmDialogService confirmDialog,
        ISandboxSupport sandboxSupport,
        ILogger<SettingsViewModel> logger)
    {
        _settingsStore = settingsStore;
        _themeService = themeService;
        _pathProvider = pathProvider;
        _storagePicker = storagePicker;
        _platform = platform;
        _accountAuth = accountAuth;
        _sessionWriter = sessionWriter;
        _debugLogBuffer = debugLogBuffer;
        _confirmDialog = confirmDialog;
        _sandboxSupport = sandboxSupport;
        _logger = logger;
        Themes = _themeService.AvailableThemes;
        LogoModeOptions =
        [
            new LogoModeOption { Mode = HomeBackgroundLogoMode.None, Label = "None" },
            new LogoModeOption { Mode = HomeBackgroundLogoMode.Square, Label = "Vintage Story square" },
            new LogoModeOption { Mode = HomeBackgroundLogoMode.Banner, Label = "Vintage Story banner" },
            new LogoModeOption { Mode = HomeBackgroundLogoMode.Custom, Label = "Custom image file" },
        ];
        ModUpdateModeOptions =
        [
            new ModUpdateModeOption
            {
                Mode = ModUpdateMode.Off,
                Label = "Off",
                Description = "Do not check ModDB for newer releases.",
            },
            new ModUpdateModeOption
            {
                Mode = ModUpdateMode.Prompt,
                Label = "Prompt",
                Description = "Check for updates and show Update available. You choose when to install.",
            },
            new ModUpdateModeOption
            {
                Mode = ModUpdateMode.Automatic,
                Label = "Automatic",
                Description = "Install newer ModDB releases for tracked mods without asking.",
            },
        ];
        LogsFolder = new FolderPathRowViewModel(fileExplorer);
        ThemesFolder = new FolderPathRowViewModel(fileExplorer);
        _debugLogBuffer.Changed += (_, _) => OnDebugLogChanged();
        RefreshDebugLog();
    }

    public IReadOnlyList<ThemeDefinition> Themes { get; }
    public IReadOnlyList<LogoModeOption> LogoModeOptions { get; }
    public IReadOnlyList<ModUpdateModeOption> ModUpdateModeOptions { get; }
    public FolderPathRowViewModel LogsFolder { get; }
    public FolderPathRowViewModel ThemesFolder { get; }

    [ObservableProperty]
    private LogoModeOption? _selectedLogoModeOption;

    public bool IsCustomLogoMode => SelectedLogoModeOption?.Mode == HomeBackgroundLogoMode.Custom;

    public bool IsSaveStatusVisible => !string.IsNullOrWhiteSpace(SaveStatusMessage);

    public bool HasAccountError => !string.IsNullOrWhiteSpace(AccountError);

    public string SignInButtonText => RequiresTotp ? "Confirm code" : "Sign in";

    partial void OnRequiresTotpChanged(bool value) => OnPropertyChanged(nameof(SignInButtonText));

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

    partial void OnWarnOnBlockedModsChanged(bool value) => ScheduleAutoSave();

    partial void OnSelectedModUpdateModeOptionChanged(ModUpdateModeOption? value)
    {
        if (value is not null)
        {
            ModUpdateMode = value.Mode;
        }

        ScheduleAutoSave();
    }

    partial void OnModUpdateModeChanged(ModUpdateMode value) => ScheduleAutoSave();

    partial void OnHomeBackgroundCustomLogoPathChanged(string value) => ScheduleAutoSave();

    partial void OnHomeBackgroundLogoOpacityChanged(double value) => ScheduleAutoSave();

    partial void OnAccountBaseUrlChanged(string value) => ScheduleAutoSave();

    partial void OnCdnBaseUrlChanged(string value) => ScheduleAutoSave();

    partial void OnModDbApiBaseUrlChanged(string value) => ScheduleAutoSave();

    partial void OnModDbDownloadBaseUrlChanged(string value) => ScheduleAutoSave();

    partial void OnVersionCatalogUrlChanged(string value) => ScheduleAutoSave();

    partial void OnLatestStableUrlChanged(string value) => ScheduleAutoSave();

    partial void OnNewsBlogUrlChanged(string value) => ScheduleAutoSave();

    partial void OnWikiBaseUrlChanged(string value) => ScheduleAutoSave();

    partial void OnServerListUrlChanged(string value) => ScheduleAutoSave();

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
        WarnOnBlockedMods = settings.WarnOnBlockedMods;
        ModUpdateMode = settings.ModUpdateMode;
        SelectedModUpdateModeOption = ModUpdateModeOptions.FirstOrDefault(o => o.Mode == settings.ModUpdateMode)
            ?? ModUpdateModeOptions.FirstOrDefault(o => o.Mode == ModUpdateMode.Prompt);
        _modUpdateOptOutModIds = settings.ModUpdateOptOutModIds?.ToList() ?? [];
        HomeBackgroundLogoMode = settings.HomeBackgroundLogoMode;
        SelectedLogoModeOption = LogoModeOptions.FirstOrDefault(o => o.Mode == settings.HomeBackgroundLogoMode)
            ?? LogoModeOptions.FirstOrDefault(o => o.Mode == HomeBackgroundLogoMode.Square);
        HomeBackgroundCustomLogoPath = settings.HomeBackgroundCustomLogoPath ?? string.Empty;
        HomeBackgroundLogoOpacity = settings.HomeBackgroundLogoOpacity;
        BindEndpoints(settings.Endpoints ?? EndpointSettings.CreateDefaults());
        SelectedTheme = Themes.FirstOrDefault(t => string.Equals(t.Id, settings.SelectedThemeId, StringComparison.OrdinalIgnoreCase))
            ?? Themes.FirstOrDefault();
        var paths = _pathProvider.GetPaths();
        LogsFolder.Bind("Logs folder", paths.LogsDirectory);
        ThemesFolder.Bind("User themes folder", paths.ThemesDirectory);
        BindSandbox(settings);
        SetSaveStatus(string.Empty);
        StatusMessage = string.Empty;
        StatusIsError = false;
        _isBinding = false;
        _ = RefreshAccountStatusAsync();
        RefreshDebugLog();
    }

    private void BindEndpoints(EndpointSettings endpoints)
    {
        AccountBaseUrl = endpoints.AccountBaseUrl;
        CdnBaseUrl = endpoints.CdnBaseUrl;
        ModDbApiBaseUrl = endpoints.ModDbApiBaseUrl;
        ModDbDownloadBaseUrl = endpoints.ModDbDownloadBaseUrl;
        VersionCatalogUrl = endpoints.VersionCatalogUrl;
        LatestStableUrl = endpoints.LatestStableUrl;
        NewsBlogUrl = endpoints.NewsBlogUrl;
        WikiBaseUrl = endpoints.WikiBaseUrl;
        ServerListUrl = endpoints.ServerListUrl;
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
        var generation = Interlocked.Increment(ref _saveGeneration);
        _ = AutoSaveAsync(token, generation);
    }

    private async Task AutoSaveAsync(CancellationToken cancellationToken, int generation)
    {
        try
        {
            IsSaving = true;
            SetSaveStatus("Saving...");
            await Task.Delay(450, cancellationToken).ConfigureAwait(true);
            await PersistSettingsAsync(generation).ConfigureAwait(true);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            SetSaveStatus("Saved");
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
                SetSaveStatus(string.Empty);
            }
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task PersistSettingsAsync(int generation)
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
            WarnOnBlockedMods = WarnOnBlockedMods,
            ModUpdateMode = SelectedModUpdateModeOption?.Mode ?? ModUpdateMode.Prompt,
            ModUpdateOptOutModIds = _modUpdateOptOutModIds,
            HomeBackgroundLogoMode = SelectedLogoModeOption?.Mode ?? HomeBackgroundLogoMode.Square,
            HomeBackgroundCustomLogoPath = string.IsNullOrWhiteSpace(HomeBackgroundCustomLogoPath)
                ? null
                : HomeBackgroundCustomLogoPath.Trim(),
            HomeBackgroundLogoOpacity = Math.Clamp(HomeBackgroundLogoOpacity, 0.02, 1.0),
            Endpoints = new EndpointSettings
            {
                AccountBaseUrl = TrimOrDefault(AccountBaseUrl, VintageStoryEndpoints.AccountBaseUrl),
                CdnBaseUrl = TrimOrDefault(CdnBaseUrl, VintageStoryEndpoints.CdnBaseUrl),
                ModDbApiBaseUrl = TrimOrDefault(ModDbApiBaseUrl, VintageStoryEndpoints.ModDbApiBaseUrl),
                ModDbDownloadBaseUrl = TrimOrDefault(ModDbDownloadBaseUrl, VintageStoryEndpoints.ModDbDownloadBaseUrl),
                VersionCatalogUrl = TrimOrDefault(VersionCatalogUrl, VintageStoryEndpoints.VersionCatalogUrl),
                LatestStableUrl = TrimOrDefault(LatestStableUrl, VintageStoryEndpoints.LatestStableUrl),
                NewsBlogUrl = TrimOrDefault(NewsBlogUrl, VintageStoryEndpoints.NewsBlogUrl),
                WikiBaseUrl = TrimOrDefault(WikiBaseUrl, VintageStoryEndpoints.WikiBaseUrl),
                ServerListUrl = TrimOrDefault(ServerListUrl, RelicLauncherEndpoints.ServerListUrl),
            },
            ProcessIsolationEnabled = ProcessIsolationEnabled,
        };

        var themeResult = _themeService.ApplyTheme(settings.SelectedThemeId);
        if (!themeResult.IsSuccess)
        {
            SetSaveStatus(themeResult.Error ?? "Theme apply failed.", true);
            return;
        }

        var save = await _settingsStore.SaveAsync(settings).ConfigureAwait(true);
        if (!save.IsSuccess)
        {
            _logger.LogWarning("Settings save failed: {Error}", save.Error);
            SetSaveStatus(save.Error ?? "Save failed.", true);
            return;
        }

        if (generation != _saveGeneration)
        {
            return;
        }

        _onChanged?.Invoke(settings);
    }

    public event EventHandler? FocusAccountRequested;

    public void RequestFocusAccount()
    {
        IsAccountSectionExpanded = true;
        FocusAccountRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetSaveStatus(string message, bool isError = false)
    {
        SaveStatusMessage = message;
        SaveStatusIsError = isError;
    }

    private static string TrimOrDefault(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
