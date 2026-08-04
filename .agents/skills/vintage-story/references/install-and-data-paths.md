# Vintage Story install and data paths

## Game install directory (binaries)

User selects one folder in Relic Settings. Valid install contains at least one of:

- `Vintagestory`
- `Vintagestory.exe`
- `Vintagestory.dll`

Typical layouts (varies by platform and Account Manager install):

- Linux: `~/.local/share/Vintagestory/` or a custom path
- Windows: `C:\Program Files\Vintagestory\` or user-chosen
- macOS: `/Applications/Vintagestory.app` contents or extracted folder

Relic only checks the configured path. It does not scan Steam library folders automatically.

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
