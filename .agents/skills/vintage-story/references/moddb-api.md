# ModDB API (v1)

Official docs: https://github.com/anegostudios/vsmoddb

Base: `https://mods.vintagestory.at/api`

| Endpoint | Purpose |
|----------|---------|
| `/api/mods?...` | Search/list. Relic uses `text`, `orderby`, `gv` (game version tag name). Do not use `gameversion` (returns empty). Full catalog has no server page size; Relic caches and paginates client-side. |
| `/api/mod/{id}` | Mod details + releases (`modid` or url alias). Includes `logofile`, `screenshots`, `text`, links. |
| `/api/tags` | Tags |
| `/api/gameversions` | Game version tags |

Download: `https://mods.vintagestory.at/download?fileid={fileid}` (prefer `mainfile` URLs from API when present).

Local mods:

- Folder: `{DataPath}/Mods/`
- Zip or directory with `modinfo.json`
- Disable by appending `.disabled` to the file/folder name
