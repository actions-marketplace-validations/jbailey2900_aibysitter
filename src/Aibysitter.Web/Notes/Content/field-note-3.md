---
title: Field note 3
date: 2026-10-03
summary: False-positive rates for every lint rule, judged by two readers on 755 rules files, and where tuning stopped.
---

I published the linter's false-positive rates because every linter has them and almost none say so. Two judges, one of them not me, read a sample of every rule's findings and called each one right or wrong. Some rules came out well. Some didn't, and three rounds of tuning got them to "better" rather than "good." I stopped there, because the next round of tuning started costing real findings, and a rule that finds nothing has a perfect false-positive rate.

The rules that still miss the bar are the opinionated ones: rationale prose and vague verbs. That's not a coincidence. Those rules encode a view about how to write for an agent, and two careful readers can disagree about whether a given line crosses it. I'd rather ship the view with the number next to it than soften the rule until nobody objects. The number is on each rule's page, and it'll change when the ruleset does.

## Data

### Corpus

Rules files in 7 formats, fetched 2026-10-03 at each repository's default-branch HEAD. Discovery: every GitHub repository linked from 14 curated lists (5,631 repositories; 5,596 cloned). One file per repository per format, empty and duplicate content dropped, up to 150 per format, 13 symlinked files excluded.

| Format | Files |
|---|---|
| CLAUDE.md | 150 |
| AGENTS.md | 150 |
| GEMINI.md | 147 |
| Copilot instructions | 126 |
| Cursor .mdc | 119 |
| .cursorrules | 45 |
| .windsurfrules | 18 |
| Total | 755 |

### Method

- Sample: up to 30 findings per rule, at most 2 per file, spread across formats. Rules with fewer findings: all of them.
- Judge A: Claude in the working session. Judge B: a separate agent, given the rule definitions and principles but not judge A's verdicts; a new agent each round.
- TP: the finding is what the rule's summary describes, on text the rule applies to. FP: the rule matched mechanically but the text is not that.
- v2: corpus pass 2, 264 findings judged, v2 rule definitions.
- v3: two fresh samples. Round 1 (seed 4, 167 findings) informed a second tuning pass; round 2 (seed 5, 162 findings, excluding all earlier samples) is the v3 measurement. No tuning after round 2.
- Principles set before v3: R001, R002, R005, R007 and R013 apply to instructions only; descriptive prose, pasted documentation and project descriptions are never true positives. Rulings on 26 split findings applied to v3 and v4 scores.
- v4: no new judging. Round-2 verdicts with rulings applied, counted over the sampled findings v4 still reports. R008 matched per file (one finding per file; its line moves with the limit).
- 95% intervals: Wilson.

### Judge agreement

| Measurement | Findings | Agreement | Cohen's kappa |
|---|---|---|---|
| v2 (pass 2) | 264 | 238 (90.2%) | 0.75 |
| v3 round 1 | 167 | 138 (82.6%) | 0.62 |
| v3 round 2, rulings applied | 162 | 151 (93.2%) | 0.82 |
| v4, rulings applied | 149 | 140 (94.0%) | 0.84 |

### False-positive rate per rule

Judge A / judge B. v2 used v2 definitions; v3 and v4 use the instruction-only principle and the rulings. v2 is not directly comparable for R001, R002, R005, R007 and R013.

| Rule | v2 | v3 | v4 | v4 95% CI (A / B) |
|---|---|---|---|---|
| R001 RationaleProse | 10/30 (33%) / 16/30 (53%) | 14/30 (47%) / 12/30 (40%) | 14/30 (47%) / 12/30 (40%) | 30–64% / 25–58% |
| R002 VagueVerbs | 7/30 (23%) / 12/30 (40%) | 11/30 (37%) / 12/30 (40%) | 11/30 (37%) / 12/30 (40%) | 22–54% / 25–58% |
| R004 FileLength | 0/30 / 0/30 | unchanged | unchanged | 0–11% (v2) |
| R005 DuplicateLines | 21/30 (70%) / 23/30 (77%) | 3/10 (30%) / 3/10 (30%) | 1/6 (17%) / 1/6 (17%) | 3–56% / 3–56% |
| R007 HedgedInstructions | 4/30 (13%) / 4/30 (13%) | 4/30 (13%) / 6/30 (20%) | 4/30 (13%) / 6/30 (20%) | 5–30% / 10–37% |
| R008 EmphasisInflation | 5/28 (18%) / 4/28 (14%) | 2/25 (8%) / 8/25 (32%) | 1/16 (6%) / 5/16 (31%) | 1–28% / 14–56% |
| R009 SecretsInRulesFile | 2/2 / 2/2 | no findings | no findings | – |
| R010 PersonaPreamble | 0/14 / 1/14 (7%) | unchanged | unchanged | 0–22% / 1–31% (v2) |
| R011 EmptySections | 13/20 (65%) / 11/20 (55%) | 0/7 / 0/7 | 0/7 / 0/7 | 0–35% / 0–35% |
| R012 UnclosedCodeFence | 0/1 / 0/1 | unchanged | unchanged | 0–79% (v2) |
| R013 ProseParagraph | 4/30 (13%) / 7/30 (23%) | 4/30 (13%) / 4/30 (13%) | 4/30 (13%) / 4/30 (13%) | 5–30% / 5–30% |
| R014 UnverifiableCrossReference | 0/2 / 0/2 | unchanged | unchanged | 0–66% (v2) |
| R015 FrontmatterFields | 0/10 / 0/10 | unchanged | unchanged | 0–28% (v2) |
| R016 ManualCursorRule | 0/7 / 0/7 | unchanged | unchanged | 0–35% (v2) |

R003 had no findings in any version. R006 (App-only) is measured in field note 1.

v3 round 2 as measured, before rulings: R002 12/30 / 13/30, R008 2/25 / 7/25; other rules as above.

### Findings and files flagged (755 files)

| Rule | Findings v2 / v3 / v4 | Files flagged v2 / v3 / v4 |
|---|---|---|
| R001 | 248 / 115 / 115 | 112 / 74 / 74 |
| R002 | 230 / 95 / 95 | 67 / 52 / 52 |
| R004 | 116 / 116 / 116 | 116 / 116 / 116 |
| R005 | 1,362 / 38 / 27 | 36 / 7 / 5 |
| R007 | 151 / 93 / 93 | 78 / 62 / 62 |
| R008 | 28 / 25 / 16 | 28 / 25 / 16 |
| R009 | 4 / 0 / 0 | 1 / 0 / 0 |
| R010 | 22 / 22 / 22 | 13 / 13 / 13 |
| R011 | 33 / 9 / 9 | 14 / 6 / 6 |
| R012 | 1 / 1 / 1 | 1 / 1 / 1 |
| R013 | 258 / 111 / 111 | 77 / 52 / 52 |
| R014 | 2 / 2 / 2 | 2 / 2 / 2 |
| R015 | 12 / 12 / 12 | 7 / 7 / 7 |
| R016 | 7 / 7 / 7 | 7 / 7 / 7 |

| | v2 | v3 | v4 |
|---|---|---|---|
| Median score | 100 | 100 | 100 |
| Grades A / B / C / D / F | 685 / 48 / 12 / 9 / 1 | 713 / 34 / 6 / 2 / 0 | 714 / 33 / 6 / 2 / 0 |

### True positives lost per change

v2 → v3: pass-2 findings both judges called TP that v3 no longer reports.

| Rule | Lost | Cause |
|---|---|---|
| R001 | 3 of 14 | 2 descriptive sentences (out of scope under the principle); 1 "never" mid-sentence in a wrapped paragraph, no longer read as an instruction |
| R002 | 5 of 18 | 3 in documentation dumps (files with 20+ MDX components); 1 method named ("using Go's idiomatic error handling"); 1 verify clause ("Verify all functions were properly migrated") |
| R005 | 4 of 6 | 2 repeated blocks (whole prompt sections pasted twice); 2 lines no longer read as instructions (a requirement line, a bold label with a URL) |
| R007 | 3 of 26 | 1 documentation dump; 1 labelled terse item ("Module-per-responsibility: one concept per file where possible"); 1 passive sentence ("styling is done with … whenever possible") |
| R008 | 0 of 23 | – |
| R011 | 0 of 6 | – |
| R013 | 3 of 22 | under a quarter of sentences are instructions |

v3 → v4:

| Rule | Lost | Cause |
|---|---|---|
| R005 | 2 of 7 both-TP in round 2 | same position under same-level headings: a second copy of a prompt section; two unrelated "Async" sections |
| R008 | 6 of 17 both-TP files in round 2; 8 of 23 pass-2 TP files | under 5 emphasis lines per 100 lines |

### What remains

Rule tuning stopped on 2026-10-03 pending dogfood or incident data.

| Rule | Remaining false positives (v4, round 2) | Status |
|---|---|---|
| R001 | descriptive sentences inside instruction list items; "because" stating a condition inside a prohibition; "reason" as a noun or field; meta text; fixture text | no change; first two listed as known limits on the rule page, suppressible in-file |
| R002 | capability and feature lists in imperative form; targets or methods the patterns don't recognise; checkable "ensure" statements | no change |
| R005 | the same step under bold labels at different positions (1 finding) | no change |
| R007 | "consider that…"; "try to find… if not found…"; a design note; "appropriate" as an adjective | no change |
| R008 | 1 file with MUST throughout and no RFC 2119 citation (both judges); 4 short files judge B calls marginal over the minimum of 3 (judge B only) | no change |
| R013 | descriptive paragraphs with one pointer or normative clause | no change |

Sample sizes: R005 n = 6 and R011 n = 7 in v4; intervals are wide. Both judges are models.
