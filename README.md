# Relic Launcher

> [!WARNING]  
> Still under heavy development and is not ready to be used. 

Unofficial desktop launcher for [Vintage Story](https://www.vintagestory.at/). Relic Launcher is not affiliated with Anego Studios.

Built with C# / .NET 10 and Avalonia 12. Targets the same desktop platforms Vintage Story supports: Windows 10+ x64, Linux x64, and macOS 13+ (x64 and arm64).

> [!NOTE]  
> This project is AI-assisted using OpenCode and Open-Weight Models. 

## Upcoming Features

- Version management
- Updating 
- Full mod support 
- Backup mods, saves/worlds or versions
- Cross-platform and Modern UI
- Sandboxing (for app itself not the game currently)

## Requirements

- .NET 10 SDK to build
- .NET 10 Desktop Runtime to run framework-dependent publishes (same family Vintage Story 1.22 uses)

## Build and run

```bash
dotnet restore RelicLauncher.sln
dotnet build RelicLauncher.sln -c Release
dotnet run --project src/RelicLauncher.App/RelicLauncher.App.csproj
```

Tests:

```bash
dotnet test RelicLauncher.sln -c Release
```

Mutation tests (Stryker.NET, validates test quality):

```bash
dotnet tool restore
dotnet stryker --config-file stryker.core.json
dotnet stryker --config-file stryker.infrastructure.json
```

Uses `RelicLauncher.Mutation.sln` (Core and Infrastructure only, no Avalonia UI projects).

Format check:

```bash
dotnet format RelicLauncher.sln --verify-no-changes
```

## Publish RIDs

```bash
dotnet publish src/RelicLauncher.App/RelicLauncher.App.csproj -c Release -r win-x64 --self-contained false -o artifacts/win-x64
dotnet publish src/RelicLauncher.App/RelicLauncher.App.csproj -c Release -r linux-x64 --self-contained false -o artifacts/linux-x64
dotnet publish src/RelicLauncher.App/RelicLauncher.App.csproj -c Release -r osx-x64 --self-contained false -o artifacts/osx-x64
dotnet publish src/RelicLauncher.App/RelicLauncher.App.csproj -c Release -r osx-arm64 --self-contained false -o artifacts/osx-arm64
```

## Config and logs

App data root:

- Windows: `%AppData%/RelicLauncher`
- Linux / macOS: `~/.config/RelicLauncher` (via `ApplicationData`)

Files:

- `settings.json` selected theme, install path, exit confirm
- `logs/relic-YYYYMMDD.log`
- `themes/` reserved for user theme packs

## Themes

Built-in packs live in `src/RelicLauncher.Themes/Themes/`. Controls bind to keys such as `Theme.Bg0`, `Theme.Accent`, and `Theme.Text`. Switch themes in Settings. Live apply swaps the merged resource dictionary.

To add a built-in pack: create an `.axaml` resource dictionary with the same keys, register it in `BuiltInThemeCatalog`, and rebuild.

## Project map

| Project | Role |
|---|---|
| `RelicLauncher.Core` | Models and interfaces. No Avalonia, no IO |
| `RelicLauncher.Infrastructure` | Settings, logging, process runner, stubs |
| `RelicLauncher.Themes` | Built-in theme resources |
| `RelicLauncher.App` | Avalonia UI, DI composition, exception bridge |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

0BSD. See [LICENSE](LICENSE).
