# Aibysitter

Babysitting the AI. Tooling for supervising AI coding agents: it reviews what an agent wrote after the fact. It does not run or constrain agents at runtime.

Live at [aibysitting.net](https://aibysitting.net). Free, no accounts.

## Rules-file linter

Lints `CLAUDE.md` / `AGENTS.md` files. Paste a file at [aibysitting.net/Lint](https://aibysitting.net/Lint).

| ID | Rule | Severity |
|---|---|---|
| R001 | RationaleProse | Info |
| R002 | VagueVerbs | Warning |
| R003 | ContradictoryModals | Error |
| R004 | FileLength (over 200 lines) | Warning |
| R005 | DuplicateLines | Warning |
| R007 | HedgedInstructions | Warning |
| R008 | EmphasisInflation (over 3 per 100 lines) | Warning |
| R009 | SecretsInRulesFile | Error |
| R012 | UnclosedCodeFence | Error |

R006 (MissingIdentifiers) is reserved for a repo-aware mode.

Suppress a rule with an HTML comment on its own line: `<!-- aibysitter-disable R002 -->` (whole file) or `<!-- aibysitter-disable-next-line R002 -->` (next line). Suppressed findings are listed and not scored.

Score: 100, minus 10 per Error, 4 per Warning, 1 per Info. Each rule deducts at most 30. Total deductions per severity are capped: Error 40, Warning 30, Info 10. Grades: A ≥ 90, B ≥ 80, C ≥ 70, D ≥ 60, F below.

Rule notes: [aibysitting.net/Notes](https://aibysitting.net/Notes).

## GitHub App

Reviews pull requests and posts a check named `Aibysitter`, with an annotation on each flagged line.

| ID | Check | Severity |
|---|---|---|
| P001 | PlaceholderIdentifiers | Error |
| P002 | TodoStubs | Warning |
| P003 | AssertNothingTests (C# only) | Error |
| P004 | OutOfScopeFiles | Error |
| P005 | SecretsInDiff | Error |
| P006 | SkippedTests | Error |
| P007 | SuppressedDiagnostics | Warning |
| P013 | CommittedArtifacts | Error |

Optional repo config, `.github/aibysitter.json`, read from the pull request's head commit:

```json
{
  "scope": ["src/**", "tests/**"],
  "conclusion": "fail-on-errors",
  "disable": ["P002"]
}
```

- `scope`: path globs from the repo root. Turns on P004. Not set: P004 is off.
- `conclusion`: `advisory` (default) reports findings as neutral. `fail-on-errors` fails the check on any Error finding.
- `disable`: check IDs to skip.

Install: coming soon. Details: [aibysitting.net/GitHub](https://aibysitting.net/GitHub).

## Gallery

Example rules files, each linted and scored: [aibysitting.net/Gallery](https://aibysitting.net/Gallery).

- `GET /gallery/{id}/{file}`: the rules file
- `GET /gallery/{id}/badge.svg`: score badge
- `GET /registry.json`: all entries, schema version 1

Entries live in [`src/Aibysitter.Web/Gallery/Content/`](src/Aibysitter.Web/Gallery/Content/), one folder per entry: `entry.json` plus the rules file.

## Repository layout

| Path | Contents |
|---|---|
| `src/Aibysitter.Rules` | Lint rules, scoring, pull request checks |
| `src/Aibysitter.Web` | ASP.NET Core Razor Pages site, GitHub App webhook, gallery |
| `tests/Aibysitter.Rules.Tests` | xUnit tests; fixtures in `tests/fixtures` |

Requires the .NET 10 SDK. Build and test: `dotnet test Aibysitter.slnx`.

## Self-hosting

See [docs/self-hosting.md](docs/self-hosting.md).

## License

- Code: MIT. See [LICENSE](LICENSE).
- Gallery content (`src/Aibysitter.Web/Gallery/Content/`): CC0 1.0. See [its LICENSE](src/Aibysitter.Web/Gallery/Content/LICENSE).
- JetBrains Mono font (`src/Aibysitter.Web/wwwroot/fonts`): SIL Open Font License 1.1. See [OFL.txt](src/Aibysitter.Web/wwwroot/fonts/OFL.txt).

## Security

See [SECURITY.md](SECURITY.md).
