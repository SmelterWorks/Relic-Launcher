# Relic Launcher testing reference

## Test projects

| Project | Tests |
|---------|-------|
| `RelicLauncher.Core.Tests` | `Result`, `PathValidator`, `VintageStoryExecutableLocator`, `BuildMetadata`, `LauncherSettings` |
| `RelicLauncher.Infrastructure.Tests` | Settings store, news parser, game locator, process runner, file explorer, URL launcher |
| `RelicLauncher.App.Tests` | `HomeBackgroundLogoResolver`, `ModInstallOrchestrator`, `TransferJobRowViewModel`, `ConfirmDialogService`, `CrashReportFormatter`, `ModInstallResult` |
| `RelicLauncher.Themes.Tests` | `BuiltInThemeCatalog` |
| `RelicLauncher.Testing` | Shared `TempAppPaths`, `FixedPathProvider`, `VintageStoryNewsHtml` fixtures (not a test project) |

## Conventions

- Use `FluentAssertions` for assertions.
- Use `TempAppPaths` for isolated settings/log paths; dispose cleans up.
- Infrastructure tests may use `internal` constructors (e.g. `VintageStoryNewsService` with `HttpClient` stub) via `InternalsVisibleTo`.
- Release builds treat warnings as errors when `CI=true`.
- Do not add tests that only assert obvious compiler behavior.

## Mutation testing (Stryker.NET)

- Tool: `dotnet-stryker` 4.16.0 in `.config/dotnet-tools.json` (required for .NET 10 / Buildalyzer 8+).
- Config: `stryker.core.json`, `stryker.infrastructure.json`
- Solution: **`RelicLauncher.Mutation.sln`** (Core + Infrastructure + test projects only). Full `RelicLauncher.sln` includes Avalonia App and breaks Stryker analysis.
- Output: `StrykerOutput/` (gitignored)
- CI: `mutation-test` job in `ci.yml` on Ubuntu

Thresholds (approximate targets):

- Core: break 50%
- Infrastructure: break 38%, excludes `FileExplorerService` and `UrlLauncher` (OS-specific process spawn)

Low mutation score means missing tests, not necessarily bad code. Add tests that would fail if the mutated line were wrong.

## Before PR

From `CONTRIBUTING.md`:

1. `dotnet test RelicLauncher.sln -c Release`
2. `dotnet format RelicLauncher.sln --verify-no-changes`
