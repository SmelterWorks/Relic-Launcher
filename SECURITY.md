# Security policy

Relic Launcher is an unofficial community project and is not affiliated with Anego Studios. Do not report security issues to them about this project.

## Reporting a vulnerability

Open a private security advisory on the GitHub repository, or email team [at] smelterworks.com. Do not file a public issue for credential or auth bugs.

Include the Relic Launcher version, OS, steps to reproduce, and impact.

## What Relic stores

- `settings.json` holds non-secret preferences (folders, theme, exit confirm). It does not store your account password.
- Account session material is stored under the app secrets directory using platform-backed protection:
  - Windows: DPAPI (CurrentUser)
  - macOS: AES envelope with a master key preferred in Keychain (`security`), file fallback with user-only permissions
  - Linux: AES envelope with a master key preferred in Secret Service (`secret-tool`), file fallback with mode `0600`
- Logs may include paths and error messages. They should not include passwords.

## Scope notes

Relic launches Vintage Story and can write a session into the game `clientsettings.json` on Play. Treat your machine account as trusted. Advanced endpoint URL overrides in Settings can repoint network calls; only change them if you understand the risk.
