# Installing the GitHub App

The App is private until launch. Only accounts it is shared with can install it.

## Install

1. Open https://github.com/apps/aibysitter/installations/new.
2. Pick the account or organization. Installing needs owner or admin rights on it.
3. Choose **Only select repositories** and pick the repositories.
4. Confirm. The App requests: Checks (read and write), Pull requests (read), Contents (read), Metadata (read).

Change the repository list later under **Configure** for Aibysitter: personal account, **Settings → Applications → Installed GitHub Apps**; organization, **Settings → GitHub Apps**.

## First pull request

The default conclusion is `advisory`. Every review completes as `success` (no findings) or `neutral` (findings). Nothing blocks a merge.

- The check is named **Aibysitter**, in the pull request's **Checks** tab.
- The summary has one row per check with its finding count, then notes and config errors.
- Each finding is an annotation on the changed line, in **Files changed**. Findings on removed files are listed in the summary.

Checks: [aibysitting.net/Notes](https://aibysitting.net/Notes).

## Config file

Optional. Path: `.github/aibysitter.json`. The App reads it from the pull request's head commit.

```json
{
  "scope": ["src/**", "tests/**"],
  "conclusion": "advisory",
  "disable": ["P012", "R013"]
}
```

| Key | Value | Default |
|---|---|---|
| `scope` | Path globs from the repository root. `**` spans folders, `*` and `?` stay within one. Case-sensitive. Turns on P004. | Not set; P004 off |
| `conclusion` | `advisory`: findings report as `neutral`. `fail-on-errors`: any Error finding fails the check. | `advisory` |
| `disable` | Check IDs (`P001`–`P014`) skip that check. Rule IDs (`R001`–`R015`) skip that rule inside P014. | None |

- Comments and trailing commas are allowed.
- An invalid entry falls back to its default and is listed under **Config errors** in the summary.
- Editing the config file in a pull request makes it a changed file: outside `scope`, it gets a P004 finding.

Starter config:

```json
{
  "conclusion": "advisory",
  "disable": ["P011", "P012"]
}
```

## Rules files

P014 lints rules files a pull request adds or changes. R006 checks the paths, scripts, and targets they name. Recognised locations:

| File | Location |
|---|---|
| `CLAUDE.md`, `AGENTS.md`, `GEMINI.md` | Any directory |
| Cursor rules | `.cursor/rules/*.mdc` |
| `.cursorrules`, `.windsurfrules` | Repository root |
| Copilot instructions | `.github/copilot-instructions.md` |

Suppress a rule inside a rules file with a comment on its own line:

```markdown
<!-- aibysitter-disable R013 -->
<!-- aibysitter-disable-next-line R006 -->
```

Precedence: comments in the file, then rule IDs in `disable`, then `"disable": ["P014"]`.

## Failing the build

1. Set `"conclusion": "fail-on-errors"`.
2. Under **Settings → Branches** (or **Rules → Rulesets**), require the **Aibysitter** status check on the default branch.
3. Disable noisy checks first: P012 (debug leftovers), P011 (CI config edited), R013 (prose paragraphs). P012 and P011 are Info; they never fail the check, but they add annotations.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| No **Aibysitter** check appears | Repository not selected, or the App lacks Checks permission | Add the repository under **Configure**; accept any pending permission request |
| Check stays **Queued** | The review was lost to a server restart | Push a commit. App owner: redeliver the webhook (App settings → Advanced → Recent deliveries) |
| Summary says "R006 skipped" | The repository file list is too large for one GitHub request | None; R006 does not run on that repository |
| Unexpected P004 findings | `scope` does not cover the path | Add the glob to `scope`, or remove `scope` |
