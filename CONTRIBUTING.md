# Contributing to Relic Launcher

## Before you open a PR

1. Run `./scripts/verify.sh` (or `dotnet test RelicLauncher.sln -c Release` and `dotnet format RelicLauncher.sln --verify-no-changes --severity error`)
2. CI also runs mutation tests on Core and Infrastructure (`dotnet tool restore` then `dotnet stryker --config-file stryker.core.json` and `stryker.infrastructure.json`). Local Stryker runs are optional but slow.
3. Keep `RelicLauncher.Core` free of Avalonia and filesystem calls
4. Put IO and process work in `RelicLauncher.Infrastructure` behind Core interfaces
5. Do not commit secrets, tokens, or personal paths

## Branch and PR shape

- One concern per PR when you can
- Describe what changed and how you tested it
- Link related issues

## Adding a theme pack

1. Add `src/RelicLauncher.Themes/Themes/YourTheme.axaml` with the same `Theme.*` resource keys as `RelicDefault.axaml`
2. Register the pack in `BuiltInThemeCatalog`
3. Include the file as an `AvaloniaResource` (wildcard already covers `Themes/**/*.axaml`)
4. Add or extend a unit test that resolves the theme id

## Code style

- Nullable reference types are on
- File-scoped namespaces
- Prefer `Result` / `Result<T>` for expected failures instead of throwing across the UI boundary
- Comments only where the why is non-obvious. No TODO markers

## AI-assisted contributions

- You are the author. Review the full diff before opening a PR. Be able to explain and defend the change in review.
- Unreviewed agent dumps and drive-by LLM patches will be closed.
- Optional: note the tool in the PR description (for example `Assisted-by: OpenCode`). Do not use `Co-authored-by` for a model.
- Maintainers use OpenCode with open-weight models the same way: human review, then the human commits and merges only if tests pass.

## Agent context

Skills for AI agents are in `.agents/skills/`. See [AGENTS.md](AGENTS.md) and [.agents/README.md](.agents/README.md). Load `relic-launcher` for repo work and `vintage-story` for game URLs, paths, and branding.

## AI Prose

Docs and issue text follow the anti-slop rules under `.agents/skills/`. Concrete headings, no filler phrases, no emdashes.

## Contact

Questions about Relic Launcher: team [at] smelterworks.com

## Security notes for workflows

Workflows pin third-party actions to full commit SHAs. Prefer `env:` for untrusted values. Do not add `pull_request_target` without a documented threat model.
