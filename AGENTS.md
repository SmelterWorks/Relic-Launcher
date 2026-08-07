# Agent guide

Relic Launcher is human-maintained. Agents edit code; humans review, commit, and ship.

## Start here

1. Load [relic-launcher](.agents/skills/relic-launcher/SKILL.md) for repo facts, build commands, and architecture rules.
2. Load [vintage-story](.agents/skills/vintage-story/SKILL.md) for official game URLs, install paths, and branding.
3. Load [no-ai-slop](.agents/skills/no-ai-slop/SKILL.md) for user-facing prose and docs.
4. Read [CONTRIBUTING.md](CONTRIBUTING.md) for PR and AI-assisted contribution rules.

Full skill index: [.agents/README.md](.agents/README.md).

## File layout conventions

Large types use **partial classes** split by concern. Do not grow a single `.cs` file past ~400 lines when the split is obvious.

| Type | Partials |
|------|----------|
| `ModsViewModel` | `.cs` (state, ctor, bind), `.Browse`, `.Installed`, `.Updates`, `.Details`, `.Media`, `.Install`, `.Tags`, `.Navigation` |
| `ModDbClient` | `.cs` (HTTP, catalog, search), `.Parse`, `.Cache` |
| `SettingsViewModel` | `.cs` (state, bind, autosave), `.Paths`, `.Account`, `.Reset`, `.Debug` |
| `ModpackService` | `.cs`, `.Apply`, `.Export`, `.Local` |

Put new logic in the smallest matching partial or extract a service under `RelicLauncher.App/Services` or `RelicLauncher.Infrastructure`.

Details: [architecture reference](.agents/skills/relic-launcher/references/architecture.md).

## After editing

Stop and give the human a short review brief (purpose, files, risks, tests run, suggested commit message). Never `git commit`, `git push`, or open PRs. See Human review in the relic-launcher skill.
