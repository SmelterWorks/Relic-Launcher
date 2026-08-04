# ModDB API (v1)

Official docs: https://github.com/anegostudios/vsmoddb

Base: `https://mods.vintagestory.at/api`

| Endpoint | Purpose |
|----------|---------|
| `/api/mods?...` | Search/list (`text`, `orderby`, `gameversion`, ...) |
| `/api/mod/{id}` | Mod details + releases (`modid` or url alias) |
| `/api/tags` | Tags |
| `/api/gameversions` | Game version tags |

Download: `https://mods.vintagestory.at/download?fileid={fileid}` (prefer full URLs from API when present).

Local mods:

- Folder: `{DataPath}/Mods/`
- Zip or directory with `modinfo.json`
- Disable by appending `.disabled` to the file/folder name
