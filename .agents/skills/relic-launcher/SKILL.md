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
5. **Reuse before duplicating**: `PathValidator`, `VintageStoryExecutableLocator`, `PageViewModelBase`, `PageHeader`, `FolderPathRow`, `HomeBackgroundLogoResolver`, `RelicLauncher.Testing` helpers.
6. **No god files**: split new features into services/controls/ViewModels. Home page already combines launch + news + logo; extend via extracted services, not more logic in one class.
7. **Comments**: only non-obvious why. No TODO markers. No emdashes in comments or docs.

## App data paths (Relic Launcher)

Resolved by `AppPathProvider`:

- Root: `%AppData%/RelicLauncher` (Windows) or `~/.config/RelicLauncher` (Linux/macOS via `ApplicationData`)
- `settings.json`, `logs/relic-YYYYMMDD.log`, `themes/` (user theme packs)

This is **Relic** config, not Vintage Story game data.

## Current product surface (do not invent beyond this)

Implemented:

- Sidebar nav: Home, Versions, Mods, Wiki, Settings, About
- Home: play (active managed version), Vintage Story blog news, optional background logo
- Versions: catalog from api.vintagestory.at, install/uninstall/set active under InstallsRoot
- Mods: ModDB browse/install (tags, blocklist warn) plus local import/enable/disable/uninstall
- Wiki: domain-locked `NativeWebView` for the configured wiki base URL (default wiki.vintagestory.at), with reachability probe and Open in browser fallback
- Settings: account sign-in (auth3 email/password + TOTP), installs root, data path, theme, logo, confirm-before-exit, service URLs including Wiki
- About: version, commit, build time, logs folder open, 0BSD
- Built-in themes: `relic-default`, `temporal-rift`, `moss-hearth`, `copper-dungeon`, `high-contrast`

Still stubbed / out of scope:

- Relic self-updater (`IUpdateCheckService`)
- Multi-instance profiles
- Steam library auto-detect

Do not add UI copy implying unimplemented features work.

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
- Reusable: `PageHeader`, `FolderPathRow`, `SettingsSection`
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
| Opening unreviewed agent dumps as PRs/issues | Human reviews the full diff first. See CONTRIBUTING.md |
| Shipping without asking for human review | Prompt for review. Present purpose, files, risks, tests |
| `git commit`, `git push`, `gh pr create`, or remote publish | Humans only. Agent stops at a review brief |
| `Co-authored-by` for a model | Optional `Assisted-by: tool` in the PR. Human is the author |

## Human review

This repo is human-maintained. Agents are an editing aid. Load [no-ai-slop](../no-ai-slop/SKILL.md) for docs and issue text. Contributor rules: `CONTRIBUTING.md` (AI-assisted contributions).

**Humans only (agents must never do these):** `git commit`, `git push`, force-push, tag, release, `gh pr create`, merge, or any remote publish. Even if the human says "commit this", refuse and hand them the review brief plus suggested commit message instead.

After code or doc edits, stop and ask the human to review. Present the change so a human can review it without reconstructing the diff themselves:

1. One-line purpose of the change
2. File list with what each file does in the change
3. Risky or non-obvious spots (auth, paths, process launch, settings schema, CI secrets)
4. Commands already run and their outcome (test, format, build)
5. What still needs a human look (UI paths, platforms not tested)
6. Suggested commit message (text only, do not run `git commit`)

Prefer a short review brief over a long narrative. Cite concrete paths and symbols. Do not claim the work is ready to ship until the human says so.

## Prose in this repo

User-facing text: follow [no-ai-slop](../no-ai-slop/SKILL.md). No emdashes, no filler, no fabricated claims about Vintage Story or Relic features.
