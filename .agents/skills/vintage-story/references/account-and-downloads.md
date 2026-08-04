# Account auth and downloads

## Game client login (used by Relic)

Same path as MVL, VS Launcher, and GruntLauncher. No webview. No captcha.

- URL: `https://auth3.vintagestory.at/v2/gamelogin`
- Form fields: `email`, `password`, `totpcode`, `prelogintoken`, `gameloginversion`
- `gameloginversion` comes from `https://api.vintagestory.at/latestunstable.txt`
- Success JSON: `valid=1` plus `sessionkey`, `sessionsignature`, `uid`, `playername`, optional `entitlements`, `mptoken`, `hasgameserver`
- 2FA: `valid=0` with `reason=requiretotpcode` and `prelogintoken`, then resubmit with `totpcode`
- Session check: `https://auth3.vintagestory.at/clientvalidate` with `uid` + `sessionkey`

Relic stores the session via `ISecretStore` (never the password). On Play, Relic writes those fields into `{DataPath}/clientsettings.json` under `stringSettings`.

This auth API is reverse-engineered from the game client. It is not an official public API. Do not invent extra endpoints.

## Client Area portal (not used for Relic sign-in)

Game account portal: `https://account.vintagestory.at/`

Form POST (fails when captcha is required):

- URL: `https://account.vintagestory.at/attemptlogin`
- Fields: `email`, `password`, `loginredir`
- Portal HTML includes Google reCAPTCHA. Direct POSTs without a captcha token return `Captcha verification failed`.

Relic does not use the portal or embedded WebView for sign-in. Other community launchers also skip this path.

## Version catalog (public)

| URL | Use |
|-----|-----|
| `https://api.vintagestory.at/stable-unstable.json` | Version map with per-platform packages |
| `https://api.vintagestory.at/lateststable.txt` | Latest stable version string |

Client package keys Relic selects:

| OS | Key |
|----|-----|
| Windows | `windows` (Inno `.exe`) |
| Linux | `linux` (`.tar.gz`) |
| macOS x64 | `mac-x64` |
| macOS arm64 | `mac-arm64` |

Skip `linuxserver`, `windowsserver`, `windowsupdate` for client installs.

CDN files live under `https://cdn.vintagestory.at/gamefiles/...` and do not need account cookies. Account-local mirrors under `https://account.vintagestory.at/files/...` may require portal cookies, but Relic prefers CDN URLs first.

## Installs layout (Relic)

```text
{InstallsRoot}/versions/{version}/
{InstallsRoot}/versions.json
{DataPath}/Mods/
```

Launch uses `--dataPath "{DataPath}"` when launching the client.
