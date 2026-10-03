# Going public

Steps in order. Repository settings paths are under `github.com/jbailey2900/aibysitter/settings`.

## Before

- [ ] GitHub Support has removed `refs/pull/1–14/head` and run garbage collection. Check from a fresh mirror clone: none of the 35 old commit SHAs is fetchable.
- [x] History secrets scan (2026-10-03): every blob reachable from the 64 refs (1,076 blobs) against `SecretPatterns`. 60 matches, all test data, documentation examples or the CI test container password. No credentials.

## At the flip

- [ ] General → Danger Zone → Change visibility → Public.
- [ ] Security → Private vulnerability reporting: on (`SECURITY.md` and the issue chooser link to it).
- [ ] Security → Secret scanning and push protection: on.
- [ ] Security → Dependabot alerts: on.
- [ ] Rules → Rulesets → new branch ruleset for `main`: require a pull request; require status checks `build-test` and `catalogs-windows`; block force pushes; restrict deletions.
- [ ] Install the Aibysitter GitHub App on `jbailey2900/aibysitter` (`CONTRIBUTING.md` says every pull request gets its check).
- [ ] Issues → Labels: create `false positive` (the False positive template applies it; `bug` and `enhancement` exist by default).
- [ ] Secrets and variables → Actions → New repository secret `NUGET_API_KEY`: a nuget.org API key with Push scope for `Aibysitter.Cli`.

## After

- [ ] General → Social preview: upload `docs/brand/social-preview-1280x640.png`.
- [ ] One pull request for the text that changes:

| File | Text now | Change to |
|---|---|---|
| `src/Aibysitter.Web/Pages/About.cshtml` | The repository is private until launch. | Source link; MIT code, CC0 gallery content |
| `src/Aibysitter.Web/Pages/Hooks.cshtml` | not yet published to NuGet | `dotnet tool install --global Aibysitter.Cli` |
| `README.md` | Not yet published to nuget.org | `dotnet tool install --global Aibysitter.Cli` |
| `README.md` | Not on the Marketplace yet | Marketplace link |
| `README.md` | jbailey2900/aibysitter@main | `jbailey2900/aibysitter@v1` |

- [ ] CLI release: on `main`, tag `cli-v<Version>` from `src/Aibysitter.Cli/Aibysitter.Cli.csproj` and push the tag. `release-cli.yml` checks the tag against `main` and `<Version>`, runs the CLI tests, packs and pushes. Check the package page on nuget.org.
- [ ] Action release: accept the GitHub Marketplace Developer Agreement (account needs 2FA). On `main`, tag `v1.0.0` and `v1`. Open `action.yml` on GitHub → Draft a release → tag `v1.0.0` → Publish this Action to the GitHub Marketplace → primary category Code quality, secondary Continuous integration.
- [ ] Project description: remove "Private until the GitHub App works".
