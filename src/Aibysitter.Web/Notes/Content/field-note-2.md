---
title: Field note 2
date: 2026-10-02
summary: Linux CI green, Windows production down. An agent-written change took this site offline; what broke, why nothing caught it, and what changed.
---

This site is built by Claude Code, running in Claude's cloud sessions. Every change goes through a pull request that I read and merge. One of those changes took the site down.

## What happened

- A change added rules packs, loaded from files embedded in the assembly. From that deploy on, `/Packs`, `/llms.txt` and `/packs/registry.json` returned 500 in production. Nobody noticed: the deploy health check requests `/health`, which does not touch the packs.
- The next change generated the gallery from the packs. That deploy passed its health check and took down `/`, `/Gallery`, `/Lint` and `/sitemap.xml`. Every request that needed the gallery failed with the same exception.
- I reverted the change on main. The error page appeared to fail too; on a Windows runner it rendered, so that part is unconfirmed.

## Cause

Embedded files are named from their folder path:

```xml
<EmbeddedResource Include="Content\**\*" LogicalName="packs/%(RecursiveDir)%(Filename)%(Extension)" />
```

`%(RecursiveDir)` uses the build machine's separator. CI builds on Linux: `packs/aspnet-web-api/01-commands.md`. The deploy builds on Windows: `packs/aspnet-web-api\01-commands.md`.

The packs loader split names on `/`. With no `/` after the prefix, it sliced past the end of the string and threw `ArgumentOutOfRangeException` in the constructor of a singleton, on every request that resolved it.

The gallery loader, written earlier in the same solution, already replaced `\` with `/`. The packs loader did not reuse it.

## Why nothing caught it

- CI ran on Linux only. Every test passed and asserted the right things, on Linux.
- Catalogs loaded on first request, not at startup, so the site started and reported healthy.
- The health check covered the process, not the content.
- The bug was a missing line. A diff review shows what was added.

Reproduced on a Windows runner: 26 catalog tests failed on main after the revert, 79 with the gallery change.

## What changed

- **One loader for embedded names.** Packs, gallery and notes read resource names through one function that normalizes separators. A malformed name is a validation error, not an exception.
- **Catalogs load at startup.** If any fails, the failure is logged and the host does not start, so the deploy health check fails instead of the site serving 500s.
- **The error page has no dependencies.** No layout, no injected services.
- **A Windows CI job.** It builds the solution and runs the catalog tests on Windows. Its first run found a second Windows-only bug: a CRLF checkout embedded the gallery files with CRLF line endings, and production had been serving them. Embedded text is now normalized to LF.
- **Automatic rollback.** Each deploy snapshots the live site when it is healthy. If the new build fails its health check, the snapshot is restored and checked again. The deploy still fails, so the failure is visible.

## Checks worth stealing

- When an agent adds code next to similar existing code, compare the two. Ask why the new one differs.
- Run CI on the operating system you deploy to, at least for anything that reads files, paths, embedded resources or line endings.
- Load at startup whatever every request depends on, and fail the start if it cannot load.
- Make the health check fail when the content is broken, not only when the process is down.
- Keep an error page that depends on nothing.
- Keep the previous build and restore it automatically.

None of Aibysitter's pull request checks would have caught this. P001 to P004 flag placeholder identifiers, TODO stubs, tests that assert nothing and files outside the declared scope; the change had none of those. It was a difference between build environments, not a pattern in the diff. Reviewing agent output after the fact has that limit.
