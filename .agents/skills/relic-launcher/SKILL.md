---
name: relic-launcher
description: Relic Launcher repo architecture, conventions, build/test commands, and anti-hallucination facts for this C# Avalonia project. Use when editing Relic Launcher code, tests, CI, UI, settings, themes, or when an agent needs project context to avoid inventing APIs or structure.
---

# Relic Launcher (project skill)

Relic Launcher is an **unofficial** community desktop launcher for [Vintage Story](https://www.vintagestory.at/). It is **not** affiliated with Anego Studios. For game-specific URLs and install layout, also load [vintage-story](../vintage-story/SKILL.md).

## Stack (verified)

| Layer | Choice |
|-------|--------|
| Language | C# / .NET 10 (`net10.0`, SDK pinned in `global.json`) |
| UI | Avalonia 12.1 |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| Logging | Serilog (file sink under app data) |
| Tests | xUnit + FluentAssertions |
| Mutation tests | Stryker.NET 4.16+ via `RelicLauncher.Mutation.sln` |
| License | 0BSD |

Display version is **`0.1.0`** (`VersionPrefix` in `Directory.Build.props`). Assembly/file version may be `0.1.0.0`. UI and `BuildMetadata.Version` use three-part `0.1.0`.

## Solution layout

| Project | Role | Rules |
|---------|------|-------|
| `RelicLauncher.Core` | Models, `Result<T>`, abstractions, path helpers | **No** Avalonia, **no** filesystem IO |
| `RelicLauncher.Infrastructure` | Settings JSON, Serilog, process runner, news fetch, stubs | Implements Core interfaces only |
| `RelicLauncher.Themes` | Built-in AXAML theme dictionaries | `Theme.*` resource keys |
| `RelicLauncher.App` | Avalonia shell, ViewModels, pages, DI entry | References Core + Infrastructure + Themes |

Test projects: `RelicLauncher.Core.Tests`, `RelicLauncher.Infrastructure.Tests`, `RelicLauncher.App.Tests`, `RelicLauncher.Themes.Tests`, shared `RelicLauncher.Testing`.

## Architecture rules (do not break)

1. **Core stays pure**: interfaces and models in `RelicLauncher.Core`. No `File.*`, `Process.*`, or Avalonia in Core.
2. **IO behind interfaces**: settings, HTTP, process start, folder open, URL open go through `ILauncherSettingsStore`, `IVintageStoryNewsService`, `IProcessRunner`, `IFileExplorerService`, `IUrlLauncher`, etc.
3. **Expected failures use `Result` / `Result<T>`**: do not throw across UI boundaries for normal errors.
4. **Stubs are intentional**: `GameLocatorStub`, `UpdateCheckServiceStub` are placeholders until real version/mod flows exist. Do not pretend they are complete.
5. **Reuse before duplicating**: `PathValidator`, `VintageStoryExecutableLocator`, `PageViewModelBase`, `PageHeader`, `FolderPathRow`, `PlaceholderPageViewModel`, `HomeBackgroundLogoResolver`, `RelicLauncher.Testing` helpers.
6. **No god files**: split new features into services/controls/ViewModels. Home page already combines launch + news + logo; extend via extracted services, not more logic in one class.
7. **Comments**: only non-obvious why. No TODO markers. No emdashes in comments or docs.

## App data paths (Relic Launcher)

Resolved by `AppPathProvider`:

- Root: `%AppData%/RelicLauncher` (Windows) or `~/.config/RelicLauncher` (Linux/macOS via `ApplicationData`)
- `settings.json`, `logs/relic-YYYYMMDD.log`, `themes/` (user theme packs)

This is **Relic** config, not Vintage Story game data.

## Current product surface (do not invent beyond this)

Implemented:

- Sidebar nav: Home, Versions, Mods, Settings, About
- Home: play bar (if install path set), Vintage Story blog news list, optional background logo
- Settings: install path, theme, logo mode/opacity/custom path, confirm-before-exit
- About: version, commit, build time, logs folder open, 0BSD
- Built-in themes: `relic-default`, `high-contrast`

Placeholder / not implemented (Versions and Mods pages say so in UI):

- Version management and game updating
- Mod browse/install
- Backups

Do not add UI copy implying these work unless you implement them.

## Key files

| Area | Location |
|------|----------|
| DI composition | `src/RelicLauncher.App/Program.cs` |
| Shell / nav | `MainWindowViewModel`, `MainWindow.axaml` |
| View mapping | `ViewLocator.cs` |
| Theme catalog | `src/RelicLauncher.Themes/BuiltInThemeCatalog.cs` |
| Settings store | `JsonLauncherSettingsStore.cs` |
| News parser | `VintageStoryNewsService.ParseArticles` (HTML scrape, not JSON API) |
| Executable lookup | `VintageStoryExecutableLocator` (candidates: `Vintagestory`, `Vintagestory.exe`, `Vintagestory.dll`) |
| Build metadata | `BuildMetadata.cs` + `Directory.Build.targets` assembly attributes |
| Shared controls | `src/RelicLauncher.App/Views/Controls/` |
| Theme keys | `src/RelicLauncher.Themes/Themes/RelicDefault.axaml` |

Details: [references/architecture.md](references/architecture.md)

## Commands

```bash
dotnet restore RelicLauncher.sln
dotnet build RelicLauncher.sln -c Release
dotnet test RelicLauncher.sln -c Release
dotnet format RelicLauncher.sln --verify-no-changes
dotnet run --project src/RelicLauncher.App/RelicLauncher.App.csproj
```

Mutation tests (use mutation solution, not full sln):

```bash
dotnet tool restore
dotnet stryker --config-file stryker.core.json
dotnet stryker --config-file stryker.infrastructure.json
```

See [references/testing.md](references/testing.md).

## UI conventions

- Icons: Optris Material Design (`mdi-*`), registered in `Program.cs`
- Reusable: `PageHeader`, `FolderPathRow`, `PlaceholderPage`
- `CornerRadius` theme key is `Theme.Radius` as `<CornerRadius>` in AXAML, not `Double`
- Background logos: bundled under `Assets/Branding/` (`vs-logo-square.png`, `vs-logo-banner.png`), attribution in `NOTICE.txt`, press kit source in vintage-story skill

## Common agent mistakes (avoid)

| Wrong | Right |
|-------|-------|
| Inventing a Vintage Story REST API for news | Scrape `https://www.vintagestory.at/blog.html/` or extend `VintageStoryNewsService` |
| Calling this the official launcher | Unofficial community project |
| Putting file IO in Core | Infrastructure + interface in Core |
| Version `0.1.0.0` in UI | `0.1.0` via `BuildMetadata` / `VersionPrefix` |
| Duplicating executable search in ViewModels | Use `IGameLocator` / `VintageStoryExecutableLocator` |
| `Theme.Radius` as double in AXAML | Use `CornerRadius` type |
| Running Stryker on full `RelicLauncher.sln` | Use `RelicLauncher.Mutation.sln` and Stryker 4.16+ |

## Prose in this repo

User-facing text: follow [no-ai-slop](../no-ai-slop/SKILL.md). No emdashes, no filler, no fabricated claims about Vintage Story or Relic features.
