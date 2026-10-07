# Contributing

## Tests
- Requirements: .NET 10 SDK; Node.js on `PATH` for the browser parity tests.
- Run: `dotnet test Aibysitter.slnx`
- SQL Server tests run when `AIBYSITTER_TEST_SQL` holds a connection string, and are skipped otherwise. In CI they and the parity tests are required.

## Pull requests
- Every pull request gets CI and the Aibysitter GitHub App's check, `Aibysitter review`.
- `.github/aibysitter.json` disables P005 here: the check docs and test fixtures contain example credentials.
- New packages need agreement in an issue first.

## Rule changes
- A change to an R rule or P check needs a measurement note in the pull request: corpus or repos, sample size, findings, false positives per finding from two judges, before and after.
- An R rule change bumps the ruleset version and adds a changelog entry; a P check change adds an App checks changelog entry.

## False positives
Report with the False positive issue template: rule ID, the line, why it is wrong.
