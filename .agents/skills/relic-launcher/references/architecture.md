# Relic Launcher architecture reference

## Core abstractions (`RelicLauncher.Core/Abstractions`)

| Interface | Purpose | Default implementation |
|-----------|---------|------------------------|
| `IGameLocator` | Resolve install path + client executable | `GameLocatorStub` |
| `IProcessRunner` | Start game process safely | `SafeProcessRunner` |
| `ILauncherSettingsStore` | Load/save `LauncherSettings` | `JsonLauncherSettingsStore` |
| `IAppPathProvider` | Relic app data paths | `AppPathProvider` |
| `IThemeCatalog` | List built-in themes | `BuiltInThemeCatalog` |
| `IThemeService` | Apply theme at runtime | `AvaloniaThemeService` (App) |
| `IVintageStoryNewsService` | Fetch blog articles | `VintageStoryNewsService` |
| `IFileExplorerService` | Open folder in OS file manager | `FileExplorerService` |
| `IUrlLauncher` | Open HTTP(S) URLs | `UrlLauncher` |
| `IUpdateCheckService` | Launcher/game updates | `UpdateCheckServiceStub` |
| `IAppLifetime` | Shutdown coordination | `AppLifetime` |

## Core models (selected)

`LauncherSettings`:

- `SelectedThemeId` (default `relic-default`)
- `GameInstallPath`
- `ConfirmBeforeExit`
- `HomeBackgroundLogoMode`: None, Square, Banner, Custom
- `HomeBackgroundCustomLogoPath`, `HomeBackgroundLogoOpacity`

`GameInstallInfo`: `InstallPath`, `ExecutableFound`, `ExecutablePath`, `DetectedVersion`

## App ViewModels

| ViewModel | Page | Notes |
|-----------|------|-------|
| `MainWindowViewModel` | Shell | Nav, `Settings`, active nav classes |
| `HomeViewModel` | Home | Play, news, logo state |
| `SettingsViewModel` | Settings | Save applies theme + persists JSON |
| `AboutViewModel` | About | `BuildMetadata`, logs row |
| `PlaceholderPageViewModel` | Versions, Mods | `Configure(title, message)` |
| `FolderPathRowViewModel` | Reused in About/Settings | Open folder command |
| `NewsArticleViewModel` | News list items | Opens URL via `IUrlLauncher` |

## Infrastructure notes

- `VintageStoryNewsService` caches blog HTML 15 minutes. Parser targets `h2.ipsType_pageTitle` article links on the official blog HTML.
- `JsonLauncherSettingsStore` uses temp file + atomic move for writes.
- `SafeProcessRunner` validates path, rejects null args, uses `UseShellExecute = false`.
- `Namespace RelicLauncher.Infrastructure.Process` conflicts with `System.Diagnostics.Process`; use `global::System.Diagnostics.Process` in that folder.

## Themes

Register new built-in themes in `BuiltInThemeCatalog` with `avares://RelicLauncher.Themes/Themes/YourTheme.axaml`. Match keys from `RelicDefault.axaml` (`Theme.Bg0`, `Theme.Accent`, `Theme.Text`, `Theme.Radius`, etc.).

## CI (`.github/workflows`)

- `ci.yml`: build/test matrix (win/linux/mac), format check on Ubuntu, mutation tests on Ubuntu, publish-dry per RID
- `release.yml`, `nightly.yml`, `codeql.yml`, dependabot

Pin third-party actions to full commit SHAs.

## Branding assets

`src/RelicLauncher.App/Assets/Branding/`:

- `vs-logo-square.png`, `vs-logo-banner.png` from [Vintage Story press kit](https://www.vintagestory.at/presskit.html/)
- `NOTICE.txt` attribution
- Avalonia URIs: `avares://RelicLauncher.App/Assets/Branding/vs-logo-square.png`
