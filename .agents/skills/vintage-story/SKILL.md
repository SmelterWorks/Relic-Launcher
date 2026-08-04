---
name: vintage-story
description: Verified Vintage Story game facts, official URLs, install/data paths, client executable names, mods platform, and branding rules for Relic Launcher. Use when implementing game launch, paths, news, mods, versions, UI copy, or any feature touching Vintage Story. Prevents inventing official APIs or launcher behavior.
---

# Vintage Story (domain skill)

Vintage Story is a survival voxel sandbox by **Anego Studios SIA** ([vintagestory.at](https://www.vintagestory.at/)). Relic Launcher is a **third-party** tool. It does not replace the official **Account Manager** for buying, downloading, or updating the game.

## Official links (use these, do not guess)

| Resource | URL |
|----------|-----|
| Main site | https://www.vintagestory.at/ |
| Blog / news | https://www.vintagestory.at/blog.html/ |
| Press kit (logos) | https://www.vintagestory.at/presskit.html/ |
| Account / downloads | https://account.vintagestory.at/ |
| Wiki | https://wiki.vintagestory.at/ |
| Mod database | https://mods.vintagestory.at/ |
| Forums | https://www.vintagestory.at/forums/ |

Full list and notes: [references/official-urls.md](references/official-urls.md)

There is **no documented public JSON API** for the blog or mod list that Relic Launcher uses today. News is parsed from blog HTML in `VintageStoryNewsService`.

## Game client (what Relic Launcher launches)

Executable candidates searched in the **install directory root** (in order):

1. `Vintagestory` (extensionless, common on Linux)
2. `Vintagestory.exe` (Windows)
3. `Vintagestory.dll` (.NET assembly entry on some installs)

Spelling is **`Vintagestory`** (one word, no space). Do not use `Vintage Story.exe` or invented binary names.

Install path is user-configured in Relic Settings (`GameInstallPath`). Relic does not auto-detect Steam paths yet (`GameLocatorStub` only validates the configured directory).

## Player data vs game install

**Game install** (binaries): user-chosen folder containing `Vintagestory` / `.exe` / `.dll`.

**Game data** (saves, mods, config) is separate, typically:

| OS | Default data root |
|----|-------------------|
| Windows | `%AppData%/VintagestoryData` |
| Linux | `~/.config/VintagestoryData` |
| macOS | `~/Library/Application Support/VintagestoryData` |

Common subfolders: `Mods/`, `Saves/`, `Playerdata/`. See [references/install-and-data-paths.md](references/install-and-data-paths.md).

Relic Launcher settings live under **`RelicLauncher`**, not `VintagestoryData`.

## Tech stack (game, for context)

- Custom C# engine (per [press kit](https://www.vintagestory.at/presskit.html/))
- Desktop: Windows, macOS, Linux
- Recent game releases use modern .NET (README notes VS 1.22 family aligns with .NET 10 runtime for framework-dependent tooling)

Do not assume Relic can hot-patch game files or inject mods without following VS mod conventions on the wiki.

## Mods (high level)

- Hosted on **VS ModDB** (mods.vintagestory.at), not Steam Workshop
- Mods are folders/files under the player's `Mods/` directory or downloaded via in-game/mod tools
- Relic **Mods** page is a placeholder; do not implement fake mod APIs

Wiki modding entry points: search wiki for "Modding" and "Installing mods".

## News in Relic Launcher

- Source: https://www.vintagestory.at/blog.html/
- Parser looks for `h2.ipsType_pageTitle` with anchor links to article URLs
- Articles open in the system browser via `IUrlLauncher`
- Respect caching (15 min) and User-Agent `RelicLauncher/<version>`

## Branding and legal copy

- Bundled logos: square and banner PNGs from press kit, stored in `Assets/Branding/`
- UI must stay unofficial: "not affiliated with Anego Studios or Vintage Story"
- Vintage Story is a trademark of Anego Studios SIA
- Do not imply endorsement by Anego Studios

Press kit assets: "Game Logo - Square", "Game Logo - Banner". See [references/branding.md](references/branding.md).

## What agents must not invent

- Official Relic or VS REST endpoints for versions, mods, or auth
- Steam App ID workflows unless explicitly added to the repo
- Server browser or multiplayer APIs (out of scope unless requested)
- Account Manager internals (proprietary downloader)
- Feature claims for version management, mod install, or backups (not shipped yet)

When unsure, cite wiki or ask the user. Prefer reading `VintageStoryNewsService` and `VintageStoryExecutableLocator` over guessing.
