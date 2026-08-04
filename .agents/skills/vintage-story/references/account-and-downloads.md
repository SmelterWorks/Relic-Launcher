# Account auth and downloads

## Login (verified)

Game account portal: `https://account.vintagestory.at/`

Form POST (legacy / may fail when captcha is required):

- URL: `https://account.vintagestory.at/attemptlogin`
- Fields: `email`, `password`, `loginredir` (empty string OK)
- Game account is separate from forum account
- The portal currently shows Google reCAPTCHA. Direct POSTs without a captcha token return `Captcha verification failed`.

Relic sign-in path:

- Open in-app `NativeWebView` to `account.vintagestory.at`
- User completes captcha + login there
- Relic imports cookies via `TryGetCookieManager` into `IAccountAuthService.ImportBrowserSessionAsync`
- Session cookies are stored via `ISecretStore` (encrypted under Relic `secrets/`). Never persist passwords.

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

CDN files live under `https://cdn.vintagestory.at/gamefiles/...`. Account-local mirrors under `https://account.vintagestory.at/files/...` may require cookies.

## Installs layout (Relic)

```text
{InstallsRoot}/versions/{version}/
{InstallsRoot}/versions.json
{DataPath}/Mods/
```

Launch uses `--dataPath "{DataPath}"` when launching the client.
