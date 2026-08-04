# Vintage Story install and data paths

## Game install directory (binaries)

Relic manages multi-version installs under Settings `InstallsRoot`:

```text
{InstallsRoot}/versions/{version}/
{InstallsRoot}/versions.json
```

Each version folder should contain at least one of:

- `Vintagestory`
- `Vintagestory.exe`
- `Vintagestory.dll`

Typical defaults (via `IRuntimePlatform`):

- Installs root: `~/Games/RelicLauncher/Vintagestory`
- Linux data: `~/.config/VintagestoryData`
- Windows data: `%AppData%\VintagestoryData`
- macOS data: `~/Library/Application Support/VintagestoryData`

`GameInstallPath` remains as a legacy/derived field pointing at the active version directory.

## Game data directory (saves, mods, settings)

Default roots (from Vintage Story community/wiki conventions; confirm on wiki if implementing path features):

| OS | Path |
|----|------|
| Windows | `%AppData%\VintagestoryData` |
| Linux | `~/.config/VintagestoryData` |
| macOS | `~/Library/Application Support/VintagestoryData` |

Common children:

| Folder | Purpose |
|--------|---------|
| `Mods/` | Installed mods |
| `Saves/` | World saves |
| `Playerdata/` | Player-specific data |
| `Cache/` | Cached assets |

Relic Launcher **does not** read or write these paths yet except indirectly if the user sets `GameInstallPath` to the wrong folder.

## Relic Launcher data (separate)

| OS | Relic root |
|----|------------|
| Windows | `%AppData%\RelicLauncher` |
| Linux / macOS | `~/.config/RelicLauncher` (via `ApplicationData`) |

Files: `settings.json`, `logs/`, `themes/`.

## Launch command

Relic uses `IProcessRunner` with no arguments today. Do not assume server mode flags unless implementing a server launcher feature.
