---
id: 1
title: Linux CI green, Windows production down
date: 2026-10-02
agent: Claude Code (Claude cloud sessions)
submitter: jbailey2900
source: /Notes/field-note-2
caught-by: none
---
## What happened

- A pull request added rules packs, loaded from files embedded in the assembly. The packs loader split resource names on `/`. A Windows build names them with `\` (`packs/aspnet-web-api\01-commands.md`), so the loader threw `ArgumentOutOfRangeException` in the constructor of a singleton.
- The gallery loader in the same solution already normalized `\` to `/`. The new loader did not reuse it.
- A second pull request generated the gallery from the packs, which put the failing loader behind the home page.

## Impact

- From the packs deploy: `/Packs`, `/llms.txt` and `/packs/registry.json` returned 500 in production.
- From the gallery deploy until the revert: `/`, `/Gallery`, `/Lint` and `/sitemap.xml` also returned 500.

## Detection

The site owner saw the outage on the live site after the gallery deploy. Both deploys passed their health checks.

## Why review and CI missed it

- CI ran on Linux only; every test passed there.
- Catalogs loaded on first request, not at startup, so the site started and reported healthy.
- The deploy health check requested `/health`, which did not touch any catalog.
- The bug was a missing line; diff review shows what was added.

## Fix

- One function reads embedded resource names for every catalog and normalizes separators. A malformed name is a validation error.
- Every catalog loads at startup; a failure stops the host, so the deploy health check fails.
- The error page has no layout or service dependencies.
- A CI job builds on Windows and runs the catalog tests. Its first run found a second Windows-only bug: embedded text with CRLF line endings. Embedded text is now normalized to LF.
- Each deploy snapshots the healthy site and restores it when the new build fails its health check.

## Reproduced by

A temporary workflow built the reverted main branch on a Windows runner, printed the embedded resource names (`packs/aspnet-web-api\01-commands.md`) and ran the catalog tests: 26 failed. With the gallery change applied, 79 failed, and the site run in Production returned 500 on the pages listed above. The same tests pass on Linux.
