# Vintage Story official URLs

Use these exact URLs in code, tests, and docs unless the user provides a newer canonical link.

## Primary

| Name | URL |
|------|-----|
| Website | https://www.vintagestory.at/ |
| Blog index | https://www.vintagestory.at/blog.html/ |
| Press kit | https://www.vintagestory.at/presskit.html/ |
| Account manager | https://account.vintagestory.at/ |
| Account portal login POST | https://account.vintagestory.at/attemptlogin |
| Game client login POST | https://auth3.vintagestory.at/v2/gamelogin |
| Game client session validate | https://auth3.vintagestory.at/clientvalidate |
| Version catalog | https://api.vintagestory.at/stable-unstable.json |
| Latest stable | https://api.vintagestory.at/lateststable.txt |
| Latest unstable | https://api.vintagestory.at/latestunstable.txt |
| CDN game files | https://cdn.vintagestory.at/gamefiles/ |
| Wiki | https://wiki.vintagestory.at/ |
| Wiki Action API | https://wiki.vintagestory.at/api.php |
| Mod DB | https://mods.vintagestory.at/ |
| Mod DB API | https://mods.vintagestory.at/api |
| Forums | https://www.vintagestory.at/forums/ |

Relic Settings can override the wiki base URL (`WikiBaseUrl`). The in-app wiki WebView only navigates on that host. Off-host links open in the system browser.

## Blog article URL shape

Observed in Relic news parser and live blog HTML:

```
https://www.vintagestory.at/blog.html/news/<slug>-r<id>/
```

Example: `https://www.vintagestory.at/blog.html/news/v1226-server-safety-patch-2-r448/`

Articles are HTML pages, not JSON.

## Press kit media (logos)

Hosted on `media.vintagestory.at`, linked from press kit page. Relic bundles:

- Square: `gamelogo-vintagestory-square.png` (full PNG linked from press kit)
- Banner: `gamelogo-vintagestory-banner.png`

Relic copies live in `src/RelicLauncher.App/Assets/Branding/`.

## Developer / company

- Developer: Anego Studios SIA (Latvia)
- Press contact from press kit: office@anegostudios.com
- Game site copyright holder: Anego Studios SIA

## Not official Relic Launcher URLs

Relic Launcher has no production website in this repo. App data is local only. Do not create `relic-launcher.com` or similar unless the user adds it.
