# Relic Launcher architecture reference

## Core abstractions (`RelicLauncher.Core/Abstractions`)

| Interface | Purpose | Default implementation |
|-----------|---------|------------------------|
| `IRuntimePlatform` | OS/arch, default paths, package key | `RuntimePlatform` |
| `IAccountAuthService` | Game account login/session (`auth3` gamelogin) | `AccountAuthService` |
| `ISecretStore` | Encrypted secret persistence | `PlatformSecretStore` |
| `IGameVersionCatalog` | Remote version list | `VintageStoryVersionCatalog` |
| `IInstalledVersionStore` | Local versions inventory | `JsonInstalledVersionStore` |
| `IGameVersionInstaller` | Download/extract/uninstall | `GameVersionInstaller` |
| `IGameLaunchService` | Resolve + launch active version | `GameLaunchService` |
| `IModDbClient` | ModDB search/details | `ModDbClient` |
| `IModReleaseResolver` | Best release for game version | `ModReleaseResolver` |
| `IModBlocklistService` | Official blocked-mods list | `ModBlocklistService` |
| `IModLibraryService` | Local mods install/toggle | `ModLibraryService` |
| `IGameLocator` | Legacy path resolve | `GameLocatorStub` |
| `IProcessRunner` | Start process safely | `SafeProcessRunner` |
| `ILauncherSettingsStore` | Load/save settings | `JsonLauncherSettingsStore` |
| `IAppPathProvider` | Relic app data paths | `AppPathProvider` |
| `IVintageStoryNewsService` | Blog articles | `VintageStoryNewsService` |
| `IUpdateCheckService` | Relic self-update (stub) | `UpdateCheckServiceStub` |

## Settings

`LauncherSettings`:

- `InstallsRoot`, `SelectedVersion`, `DataPath`
- `GameInstallPath` (legacy/derived)
- Theme + home logo fields
- Account session is **not** in settings JSON (secrets store)

Layout: `{InstallsRoot}/versions/{version}/`, mods in `{DataPath}/Mods/`.

## App ViewModels

| ViewModel | Page |
|-----------|------|
| `HomeViewModel` | Play via `IGameLaunchService`, news |
| `VersionsViewModel` | Catalog + install/uninstall/set active |
| `ModsViewModel` | Browse ModDB + manage installed |
| `WikiViewModel` | Domain-locked wiki `NativeWebView` + reachability probe |
| `SettingsViewModel` | Account sign-in, paths, theme |
| `AboutViewModel` | Build metadata |

## Architecture notes

- Account login POSTs to `auth3.vintagestory.at/v2/gamelogin` (email/password + optional TOTP). Session fields are written into `clientsettings.json` on Play.
- Version JSON from `api.vintagestory.at/stable-unstable.json`
- Client packages download from public CDN URLs without portal cookies
- Windows client packages are Inno installers (`/VERYSILENT /DIR=...`)
- Linux/macOS prefer `.tar.gz` client archives
- Launch args include `--dataPath`
- `Namespace RelicLauncher.Infrastructure.Process` conflicts with `System.Diagnostics.Process`; use `global::` prefix
