# Changelog

All notable changes to Relic Launcher are listed here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0]

### Added

- **Backup** page: zip and restore mods, worlds, and installed game versions.
- **Hosting** page: install and run a local Vintage Story server on Windows and Linux, with start/stop/restart and a live console. Cloud tab shows SmelterWorks plans (purchase not wired yet).
- **Modpacks**: save, export, import, and apply packs, including offline `.relicmodpack` files.
- Mod update checks and installs from ModDB.
- Linux **Flatpak** builds on GitHub Releases.
- Windows **NSIS installer** and single-file portable zip alongside the folder zip.
- `--self-check` CLI for headless smoke tests in CI.
- **Servers** page: browse the public server list, filter and sort, join by direct address or LAN, save favorites and recents.
- LAN server discovery on the local network from the Servers page.
- Process isolation for the launcher, game client, and local dedicated server. Toggle in Settings; changing installs or data paths needs a restart when this is on.
- Relic Launcher self-updates: startup check, toast when a build is available, stable or nightly channel, and off/prompt modes in Settings.

### Changed

- General UI polish across the app.
- Mods page loads installed mod details and screenshots more reliably.
- `--self-check` version catalog probe tolerates transient API errors more reliably.
- GitHub release notes include SHA256 checksums for release artifacts.

### Fixed

- Wiki page no longer crashes in the Flatpak build (GNOME Platform with WebKitGTK).
- Service provider disposes correctly when the sandbox broker host is registered.

## [0.1.0] - 2026-08-05

First public release.

### Added

- Desktop launcher for Vintage Story on Windows, Linux, and macOS.
- **Home**: play the active game version, read official blog news (with images), optional background logo.
- **Versions**: browse the official catalog, install, uninstall, and set the active build under your installs folder.
- **Mods**: browse ModDB, install and manage local mods, filter by tags, warn on the official blocklist, audit dependencies, and pull in missing mods from ModDB.
- **Wiki**: in-app browser locked to the configured wiki URL (default wiki.vintagestory.at).
- Vintage Story account sign-in (email/password, TOTP, browser login). Session is reused so you skip the in-game login when launching from the launcher.
- **Settings**: installs root, shared data path, built-in themes, logo mode, confirm before exit, service URL overrides.
- Auto-download of a portable .NET 7/8/10 runtime for the game when needed.
- Five built-in themes: Relic Default, Temporal Rift, Moss Hearth, Copper Dungeon, High Contrast.
- **About**: version, commit, build time, open logs folder.
- Crash reports on unexpected errors.
- Release packages: Windows zip, Linux deb/rpm/AppImage/Arch pkg, macOS app bundle. Nightly CI builds.

[0.2.0]: https://github.com/SmelterWorks/Relic-Launcher/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/SmelterWorks/Relic-Launcher/releases/tag/v0.1.0
