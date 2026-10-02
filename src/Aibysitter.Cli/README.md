# aibysitter

Lints rules files for AI coding agents: CLAUDE.md, AGENTS.md, GEMINI.md, Cursor rules (`.cursor/rules/*.mdc`, `.cursorrules`), `.github/copilot-instructions.md`, `.windsurfrules`. Same rules, ruleset version and scoring as [aibysitting.net](https://aibysitting.net). Runs offline.

```
aibysitter lint <file|-> [--format <name>] [--disable R002,R005] [--json] [--fail-on-error] [--fail-below <A|B|C|D>]
```

Exit codes: 0 ok, 1 threshold failed, 2 usage error, 3 file not readable.

Rules: [aibysitting.net/Rules](https://aibysitting.net/Rules).
