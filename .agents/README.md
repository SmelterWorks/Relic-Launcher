# Agent context for Relic Launcher

Load the skills in `.agents/skills/` before making changes. They contain verified project facts, official Vintage Story URLs, and conventions that are not obvious from code alone.

| Skill | Use when |
|-------|----------|
| [relic-launcher](skills/relic-launcher/SKILL.md) | Any code, tests, CI, or architecture work in this repo |
| [vintage-story](skills/vintage-story/SKILL.md) | Game install paths, official links, branding, mods, or VS-specific behavior |
| [no-ai-slop](skills/no-ai-slop/SKILL.md) | Writing or editing user-facing prose, docs, issues, PR text |
| [rossmann-voice](skills/rossmann-voice/SKILL.md) | Only when explicitly asked for that voice (not default) |

Do not invent official Vintage Story APIs, launcher features, or URLs. If a fact is not in these skills or the repo, say so and ask or look it up.

Human review and git rules: `CONTRIBUTING.md` and Human review in [relic-launcher](skills/relic-launcher/SKILL.md). After edits, prompt the human to review and give a short reviewable brief (purpose, files, risks, tests run, suggested commit message). Agents must never `git commit`, `git push`, create PRs, tag, release, or publish. Humans do those.
