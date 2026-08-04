# Relic Launcher architecture reference

## Core abstractions (`RelicLauncher.Core/Abstractions`)

| Interface | Purpose | Default implementation |
|-----------|---------|------------------------|
| `IRuntimePlatform` | OS/arch, default paths, package key | `RuntimePlatform` |
| `IAccountAuthService` | Game account login/session | `AccountAuthService` |
| `ISecretStore` | Encrypted secret persistence | `FileSecretStore` |
| `IGameVersionCatalog` | Remote version list | `VintageStoryVersionCatalog` |
| `IInstalledVersionStore` | Local versions inventory | `JsonInstalledVersionStore` |
| `IGameVersionInstaller` | Download/extract/uninstall | `GameVersionInstaller` |
| `IGameLaunchService` | Resolve + launch active version | `GameLaunchService` |
| `IModDbClient` | ModDB search/details | `ModDbClient` |
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
| `SettingsViewModel` | Account sign-in, paths, theme |
| `AboutViewModel` | Build metadata |

## Infrastructure notes

- Account login POSTs to `attemptlogin` with email/password
- Version JSON from `api.vintagestory.at/stable-unstable.json`
- Windows client packages are Inno installers (`/VERYSILENT /DIR=...`)
- Linux/macOS prefer `.tar.gz` client archives
- Launch args include `--dataPath`
- `Namespace RelicLauncher.Infrastructure.Process` conflicts with `System.Diagnostics.Process`; use `global::` prefix
