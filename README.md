# Aibysitter

Babysitting the AI. Tooling for supervising AI coding agents: it reviews what an agent wrote after the fact. It does not run or constrain agents at runtime.

Live at [aibysitting.net](https://aibysitting.net). Free, no accounts.

## Rules-file linter

Lints `CLAUDE.md`, `AGENTS.md`, and `GEMINI.md` (any directory), Cursor rules (`.cursor/rules/*.mdc`, root `.cursorrules`), `.github/copilot-instructions.md`, and root `.windsurfrules`. The format is auto-detected or chosen on the page. Frontmatter is skipped by every rule except R009, R015 and R016. Paste a file at [aibysitting.net/Lint](https://aibysitting.net/Lint). With JavaScript on, the file is linted in the browser and not sent; without it, the server lints it. A public repository can be linted by URL: the first of CLAUDE.md, AGENTS.md, .github/copilot-instructions.md, GEMINI.md, .cursorrules, .windsurfrules on its default branch, fetched from raw.githubusercontent.com.

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
| R010 | PersonaPreamble | Info |
| R011 | EmptySections | Warning |
| R012 | UnclosedCodeFence | Error |
| R013 | ProseParagraph (over 80 words) | Info |
| R014 | UnverifiableCrossReference | Info |
| R015 | FrontmatterFields (Cursor .mdc only) | Warning |
| R016 | ManualCursorRule (Cursor .mdc only) | Info |

R006 (MissingIdentifiers, Warning) is App-only and runs through P014: it checks that paths, package scripts, make targets, and MSBuild targets a rules file names exist in the repository.

Suppress a rule with an HTML comment on its own line: `<!-- aibysitter-disable R002 -->` (whole file) or `<!-- aibysitter-disable-next-line R002 -->` (next line). Suppressed findings are listed and not scored.

Aibysitter lint score: 100, minus 10 per Error, 4 per Warning, 1 per Info. Each rule deducts at most 30. Total deductions per severity are capped: Error 40, Warning 30, Info 10, so the lowest possible score is 20. Grades: A ≥ 90, B ≥ 80, C ≥ 70, D ≥ 60, F below.

Rules: [aibysitting.net/Rules](https://aibysitting.net/Rules). Ruleset version and changes: [aibysitting.net/Rules/Changelog](https://aibysitting.net/Rules/Changelog). Methodology: [aibysitting.net/Rules/Methodology](https://aibysitting.net/Rules/Methodology).

## GitHub App

Reviews pull requests and posts a check named `Aibysitter review`, with an annotation on each flagged line.

| ID | Check | Severity |
|---|---|---|
| P001 | PlaceholderIdentifiers | Error |
| P002 | TodoStubs | Warning |
| P003 | AssertNothingTests (C# only) | Error |
| P004 | OutOfScopeFiles | Error |
| P005 | SecretsInDiff | Error |
| P006 | SkippedTests | Error |
| P007 | SuppressedDiagnostics | Warning |
| P008 | DeletedTests (test files) | Error |
| P009 | SwallowedExceptions | Warning |
| P010 | NewDependencies | Warning |
| P011 | CiConfigEdited | Info |
| P012 | DebugLeftovers | Info |
| P013 | CommittedArtifacts | Error |
| P014 | RulesFileLint (R001–R016 on changed rules files) | Per rule |

Optional repo config, `.github/aibysitter.json`, read from the pull request's head commit:

```json
{
  "scope": ["src/**", "tests/**"],
  "conclusion": "fail-on-errors",
  "disable": ["P002"],
  "comment": true
}
```

- `scope`: path globs from the repo root. `**` must be a whole segment; `{a,b}`, `[...]`, `!` and `\` are rejected. Turns on P004. Not set: P004 is off.
- `conclusion`: `advisory` (default) reports findings as neutral. `fail-on-errors` fails the check on any Error finding or config error.
- `disable`: check IDs to skip, and rule IDs (R001–R016) to skip inside P014.
- `comment`: `true` posts one PR comment with the summary and up to 25 linked findings, updated on each new commit. Default `false`.

Install: [docs/installing-on-your-repos.md](docs/installing-on-your-repos.md). The App is private until launch. Details: [aibysitting.net/GitHub](https://aibysitting.net/GitHub).

## API

`POST /api/lint` with JSON `{ "content": "...", "format": "Auto", "disable": ["R013"] }` returns findings, suppressed findings, score, grade, detected format and ruleset version. Same limits as the lint page; no key. Details: [aibysitting.net/API](https://aibysitting.net/API).

## CLI

`aibysitter`, a .NET tool using the same rules, ruleset version and scoring as the site. Runs offline. Not yet published to nuget.org; build and install from source:

```
dotnet pack src/Aibysitter.Cli -o artifacts
dotnet tool install --global Aibysitter.Cli --add-source artifacts
```

```
aibysitter lint <file|-> [--format <name>] [--disable R002,R005] [--json] [--fail-on-error] [--fail-below <A|B|C|D>]
aibysitter init --packs starter,aspnet-web-api --format claude [--title <text>] [--output <path>] [--force]
aibysitter packs
```

- Format: `--format`, else from the file path (as in the list above), else detected from content. `-` reads standard input.
- `--json`: the `/api/lint` response fields, plus `file`.
- `init` writes a rules file composed from rules packs (below), then prints its score. `--format`: claude, agents, gemini, copilot, cursor, cursorrules, windsurf. Default output: where that format goes (`CLAUDE.md`, `.github/copilot-instructions.md`, `.cursor/rules/<pack>.mdc`, ...). An existing file needs `--force`.
- Exit codes: 0 ok, 1 a `--fail-*` threshold failed, 2 usage error, 3 file not readable or not writable. Without a `--fail-*` flag the exit code is 0 whatever the findings.
- No length limit.

## Rules packs

Sections for a rules file, grouped by stack: [aibysitting.net/Packs](https://aibysitting.net/Packs), with each section's lint score. Machine-readable: `GET /packs/registry.json` (schema version 1). Content is CC0.

A pack is a folder under [`src/Aibysitter.Packs/Content/`](src/Aibysitter.Packs/Content/):

- `pack.json`: `schemaVersion` (1), `id` (kebab-case, the folder name), `title`, `description`, `tags`, `targets` (formats the pack is written for), `license`, optional `standalone` (`true`: the pack is used only on its own; `starter` is one).
- `intro.md` (optional): text under the H1, written only when the pack is used alone.
- `NN-name.md`: one section each, in file order. One H2 heading and its body; no H1.

Pack text never names a rules file directly; it writes `{{rules-file}}`, replaced with the output path (`CLAUDE.md`, `.github/copilot-instructions.md`, `.cursor/rules/<pack>.mdc`, ...). Loading rejects literal rules-file names and unknown `{{...}}` tokens.

Composition: `# title`, the intro when one pack is chosen, then sections in pack order. A heading in more than one pack becomes one section at its first position, with later packs' lines added and repeated list items dropped. Cursor `.mdc` output gets `description` and `alwaysApply: true` frontmatter.

## GitHub Action

`action.yml` lints rules files with the CLI and posts a check run named `Aibysitter rules` with an annotation per finding. Not on the Marketplace yet; while the repository is private, other repositories of the same owner can use it once Settings → Actions → General → Access allows it.

```yaml
permissions:
  contents: read
  checks: write
steps:
  - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
  - uses: jbailey2900/aibysitter@main
    with:
      fail-on-error: "true"
```

| Input | Default | |
|---|---|---|
| `files` | empty | Paths or globs, space- or newline-separated. Empty: the supported rules files tracked in the repository. |
| `fail-on-error` | `false` | Fail when any Error finding remains. |
| `fail-below` | empty | Fail when a file's grade is below A, B, C or D. |
| `token` | `github.token` | Needs `checks: write`. Without it, annotations are written to the log (GitHub shows 10 per type per step) and the job summary lists all findings. |

Outputs: `grade` and `score` (lowest across files), `findings` (total), `report` (path to the merged JSON).

Check conclusion: `failure` when a threshold fails, `neutral` with findings, `success` with none or no files. Annotation levels: Error → failure, Warning → warning, Info → notice. The CLI is built from source on each run (about a minute) until it is published.

## Score history and badges

Lint by URL stores each public result: repository, file, score, grade, ruleset version and time. The result page lists the last 10 for that repository and file. Kept up to 400 days, at most 500 rows per repository and file. Private repositories cannot be read, so nothing is stored for them.

Badge: `https://aibysitting.net/badge/{owner}/{repo}.svg`, optional `?file=` (one of the supported file names). Default: the first file found, in the lint-by-URL order. Cached for an hour; a refresh fetches, lints and stores a row. No file found: grey `unknown`.

## Gallery

Example rules files, each linted and scored: [aibysitting.net/Gallery](https://aibysitting.net/Gallery).

- `GET /gallery/{id}/{file}`: the rules file
- `GET /gallery/{id}/badge.svg`: score badge
- `GET /registry.json`: all entries, schema version 1

Entries live in [`src/Aibysitter.Web/Gallery/Content/`](src/Aibysitter.Web/Gallery/Content/), one folder per entry holding only `entry.json`: metadata, `file` (and `path` for `.mdc`), `pack`, and optional `title`. The content is that rules pack composed in the entry's format, so the gallery and `aibysitter init --packs <pack> --format <format>` produce the same file.

## Repository layout

| Path | Contents |
|---|---|
| `src/Aibysitter.Rules` | Lint rules, scoring, pull request checks |
| `src/Aibysitter.Cli` | `aibysitter` dotnet tool |
| `src/Aibysitter.Packs` | Rules packs (content), loader, validation, composer |
| `action.yml`, `action/` | GitHub Action wrapping the CLI; fixtures in `tests/action-fixtures` |
| `src/Aibysitter.Rules.Browser` | Build-time exporter: rule patterns and constants for the browser lint engine |
| `src/Aibysitter.Web` | ASP.NET Core Razor Pages site, GitHub App webhook, gallery, notes (`Notes/Content`); browser lint engine in `wwwroot/js` |
| `tests/Aibysitter.Rules.Tests` | xUnit tests; fixtures in `tests/fixtures` |

Requires the .NET 10 SDK. Build and test: `dotnet test Aibysitter.slnx`. The browser parity tests need Node.js on PATH: without it they pass with a SKIPPED message locally and fail when `CI` is set.

## Privacy

What the site and the App read, keep, and log: [aibysitting.net/Privacy](https://aibysitting.net/Privacy).

## Self-hosting

See [docs/self-hosting.md](docs/self-hosting.md).

## License

- Code: MIT. See [LICENSE](LICENSE).
- Gallery content (`src/Aibysitter.Web/Gallery/Content/`): CC0 1.0. See [its LICENSE](src/Aibysitter.Web/Gallery/Content/LICENSE).
- JetBrains Mono font (`src/Aibysitter.Web/wwwroot/fonts`): SIL Open Font License 1.1. See [OFL.txt](src/Aibysitter.Web/wwwroot/fonts/OFL.txt).

## Security

See [SECURITY.md](SECURITY.md).
