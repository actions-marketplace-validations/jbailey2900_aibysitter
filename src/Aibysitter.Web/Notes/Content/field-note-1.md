---
title: Field note 1
date: 2026-10-01
summary: Rule hit rates across 336 public CLAUDE.md and AGENTS.md files, and stale references in 384 rules files.
---

> Introduction to be written.

## Data

### Method

The corpus is every `CLAUDE.md` and `AGENTS.md` linked from the awesome-claude-code and awesome-claude-md lists, fetched at HEAD on 2026-10-01 and deduplicated by content: 336 files. For R006, the corpus is 384 rules files from 305 public repos, including Copilot, Cursor and Gemini files, each checked against a shallow clone of its repository at HEAD.

### Rule hit rates, before and after B0

B0 changed R002 (VagueVerbs) and R005 (DuplicateLines) only. 336 files.

| Rule | Severity | Files flagged, before | Files flagged, after | Findings, before | Findings, after |
|---|---|---|---|---|---|
| R001 RationaleProse | Info | 96 (28.6%) | 96 (28.6%) | 269 | 269 |
| R002 VagueVerbs | Warning | 156 (46.4%) | 48 (14.3%) | 436 | 90 |
| R003 ContradictoryModals | Error | 0 (0.0%) | 0 (0.0%) | 0 | 0 |
| R004 FileLength | Warning | 129 (38.4%) | 129 (38.4%) | 129 | 129 |
| R005 DuplicateLines | Warning | 72 (21.4%) | 15 (4.5%) | 373 | 24 |

| | Before | After |
|---|---|---|
| Median score | 96 | 99 |
| Grade A | 232 | 308 |
| Grade B | 47 | 22 |
| Grade C | 43 | 4 |
| Grade D | 14 | 2 |
| Grade F | 0 | 0 |

R002 by term (files / findings):

| Term | Before | After |
|---|---|---|
| ensure | 74 / 123 | 9 / 12 |
| handle | 57 / 90 | 16 / 20 |
| appropriate | 45 / 85 | 19 / 25 |
| manage | 27 / 33 | 2 / 4 |
| improve | 22 / 24 | 2 / 2 |
| properly | 22 / 26 | 9 / 9 |
| as needed | 18 / 21 | 6 / 7 |
| optimize | 14 / 21 | 2 / 2 |
| clean up | 14 / 17 | 7 / 7 |
| appropriately | 7 / 10 | 5 / 7 |

### R006: stale references

384 files, 305 repos.

| Pass | References extracted | Files flagged |
|---|---|---|
| First | 13,743 | 228 (59.4%) |
| Final | 5,646 | 46 (12.0%) |

Final-pass references by kind: paths 4,268, package scripts 1,051, make targets 327.

The final pass produced 97 findings in 46 files: 23 `CLAUDE.md`, 21 `AGENTS.md`, 1 `.github/copilot-instructions.md`, 1 `.cursorrules`. Each file was judged by hand: 32–35 contain at least one stale reference; 11–14 (2.9–3.6% of the corpus) contain only false positives.

Examples, checked against the clones at HEAD:

| Repository | File | Line | Reference | At HEAD |
|---|---|---|---|---|
| gaearon/overreacted.io | `CLAUDE.md` | 34, 56 | `app/posts.js` | Not present |
| gaearon/overreacted.io | `CLAUDE.md` | 46 | `app/page.js` | Not present; `app/page.tsx` exists |
| mozilla/pdf.js | `AGENTS.md` | 118 | `web/viewer.mjs` | Not present; `web/viewer.js` exists |
| oven-sh/bun | `CLAUDE.md` | 142 | `src/shell/` | Not present; `src/shell_parser/` exists |
| huggingface/transformers | `.github/copilot-instructions.md` | 17, 26 | `make fixup` | No `fixup` target in the Makefile |
| calcom/cal.com | `AGENTS.md` | 130 | `packages/features/ee/workflows/lib/constants.ts` | Not present; no `packages/features/ee/` folder |
| (one repository) | `CLAUDE.md` | 11, 12, 109, 124 | `npm run check-version`, `npm run deploy` | No such scripts in `package.json` |
