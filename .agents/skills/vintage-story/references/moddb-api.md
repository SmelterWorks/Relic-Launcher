# ModDB API (v1)

Official docs: https://github.com/anegostudios/vsmoddb

Base: `https://mods.vintagestory.at/api`

| Endpoint | Purpose |
|----------|---------|
| `/api/mods?...` | Search/list. Relic uses `text`, `orderby`, `gv` (game version tag name). Do not use `gameversion` (returns empty). Full catalog has no server page size; Relic caches and paginates client-side. |
| `/api/mod/{id}` | Mod details + releases (`modid` or url alias). Includes `logofile`, `screenshots`, `text`, links. |
| `/api/tags` | Tags |
| `/api/gameversions` | Game version tags (`tagid`, `name`, `color`). Names match release `tags` and search `gv=`. |

Download: `https://mods.vintagestory.at/download?fileid={fileid}` (prefer `mainfile` URLs from API when present).

## Releases and game versions

Each release on `/api/mod/{id}` includes:

- `fileid`, `modversion`, `filename`, `mainfile`
- `tags`: **compatible game versions** as an explicit list of version name strings (for example `1.22.0`, `1.22.1`, … `1.22.6`)

One zip can list many game versions. There is no range syntax. Authors attach exact tags. Relic maps `tags` to `CompatibleGameVersions` and uses them when picking a release for the active game version.

## V2 install resolver (in development upstream)

`GET /api/v2/mods/install-information?ids={modinfoId}&gv={gameVersion}` resolves the best zip for a game version. Live responses include `fileName`, `fileUrl` (path like `/download/{fileid}/...`), and optional `recommendedUpgrade`. Relic prefers this for “install for active version,” then falls back to v1 release `tags` if v2 fails. Do not build a full v2 client beyond this resolver.

Local mods:

- Folder: `{DataPath}/Mods/`
- Zip or directory with `modinfo.json`
- Disable by appending `.disabled` to the file/folder name
- Relic keeps one enabled release per `modid` and caches downloads under Relic `cache/mods/files/{fileid}.zip`
