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
| `ModsViewModel` | Mods page (partials under `ViewModels/ModsViewModel.*.cs`) |
| `WikiViewModel` | Domain-locked wiki `NativeWebView` + reachability probe |
| `SettingsViewModel` | Account sign-in, paths, theme (partials under `ViewModels/SettingsViewModel.*.cs`) |
| `AboutViewModel` | Build metadata |

## Architecture notes

- Account login POSTs to `auth3.vintagestory.at/v2/gamelogin` (email/password + optional TOTP). Session fields are written into `clientsettings.json` on Play.
- Version JSON from `api.vintagestory.at/stable-unstable.json`
- Client packages download from public CDN URLs without portal cookies
- Windows client packages are Inno installers (`/VERYSILENT /DIR=...`)
- Linux/macOS prefer `.tar.gz` client archives
- Launch args include `--dataPath`
- `Namespace RelicLauncher.Infrastructure.Process` conflicts with `System.Diagnostics.Process`; use `global::` prefix

## Partial class layout (maintainability)

When a type grows past ~400 lines, split by concern. Match existing naming: `TypeName.Concern.cs`, all `partial`, same namespace.

### `ModsViewModel` (`RelicLauncher.App/ViewModels/`)

| File | Responsibility |
|------|----------------|
| `ModsViewModel.cs` | Fields, observable state, ctor, `Bind`, paging hooks, `PersistSettingsAsync`, transfers |
| `ModsViewModel.Browse.cs` | ModDB search, pagination |
| `ModsViewModel.Installed.cs` | Installed list, filters, duplicates, dependency rows |
| `ModsViewModel.Updates.cs` | Update check, apply, opt-out |
| `ModsViewModel.Details.cs` | Open mod, releases, blocklist warning |
| `ModsViewModel.Media.cs` | Logos, screenshots, image viewer |
| `ModsViewModel.Install.cs` | Install plan, uninstall, toggle, import |
| `ModsViewModel.Tags.cs` | Tag chips and filters |
| `ModsViewModel.Navigation.cs` | Open folder, ModDB page, URLs |

`ModpackPanelViewModel` stays separate (embedded in Mods page).

### `ModDbClient` (`RelicLauncher.Infrastructure/Mods/`)

| File | Responsibility |
|------|----------------|
| `ModDbClient.cs` | HTTP, catalog refresh, search orchestration, filtering |
| `ModDbClient.Parse.cs` | JSON parse helpers (`ParseSearch`, `ParseDetails`, etc.) |
| `ModDbClient.Cache.cs` | Disk cache read/write |

### `SettingsViewModel` (`RelicLauncher.App/ViewModels/`)

| File | Responsibility |
|------|----------------|
| `SettingsViewModel.cs` | State, `Bind`, autosave, `PersistSettingsAsync` |
| `SettingsViewModel.Paths.cs` | Folder/image pickers |
| `SettingsViewModel.Account.cs` | Sign-in, sign-out, session status |
| `SettingsViewModel.Reset.cs` | Reset settings and endpoint URLs |
| `SettingsViewModel.Debug.cs` | In-app debug log viewer |

### `ModpackService` (`RelicLauncher.Infrastructure/Modpacks/`)

Already split: `ModpackService.cs`, `.Apply`, `.Export`, `.Local`.

Agent entry point for this index: [AGENTS.md](../../../../AGENTS.md) at repo root.
