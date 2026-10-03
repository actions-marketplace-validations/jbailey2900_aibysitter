namespace Aibysitter.Rules;

/// <param name="Version">Ruleset version; null for App-check entries, which do not bump it.</param>
public sealed record ChangelogEntry(int? Version, DateOnly Date, string Title, IReadOnlyList<string> Changes);

/// <summary>What changed in each ruleset version, and in the App's checks. Newest first.</summary>
public static class RulesetChangelog
{
    private static readonly DateOnly Launch = new(2026, 10, 1);

    public static IReadOnlyList<ChangelogEntry> Ruleset { get; } =
    [
        new(3, new DateOnly(2026, 10, 3), "Instruction lines only; precision fixes from corpus pass 2", [
            "Instruction sentence: a clause opens with a directive word or an imperative verb, or a label opens with a directive word; or it contains must, should, shall, need to, have to, is required, you can / could / may / might, or do not after no subject pronoun; or it is a short list item with no label, no verb form and no leading determiner, pronoun or gerund (\"- One concept per file.\"). Catalog entries (list items labelled with inline code or an identifier) and files with 20 or more MDX component lines are not instructions.",
            "R001: only in a list item that contains an instruction sentence, or in an instruction sentence and the sentence after it. Skips quoted text, \"just / only / simply / merely / solely / purely because\", \"because of\", and \"the reason\" as an object.",
            "R002: instruction lines only. Skips a named resource after clean up / handle / manage; a stated purpose or method after the verb; \"ensure\" after inline code or before \"to avoid / prevent\"; qualifiers in verify / check clauses, followed by a gerund or a purpose or condition, or after more / most / less / least; \"as needed\" after inline code.",
            "R005: repeated instruction lines only. Headings, HTML comments, wrapped continuation lines and indented code are not counted. Lines that occur three or more times, and repeats in blocks of the same shape, are not reported.",
            "R007: instruction lines only. Skips \"can / could / may / might / would / will try to\", \"prefer to\" with a named alternative, and \"consider\" used as a label.",
            "R008: MUST and REQUIRED are not counted in files that use RFC 2119 keywords.",
            "R009: pass, passwd, pwd, abc123, 123456, 12345678 and qwerty are placeholders.",
            "R011: a level-1 heading with content after a heading is content (embedded document). Exempt: headings that are instructions of four or more words or contain inline code; headings after a line ending in \":\"; adjacent same-level heading lines in .cursorrules and .windsurfrules.",
            "R013: only paragraphs in which at least a quarter of the sentences are instructions.",
        ]),
        new(2, Launch, "Manual Cursor rules move to R016 (Info)", [
            "R016 ManualCursorRule (Info): a Cursor rule with alwaysApply not true, no globs and no description. Message: \"Manual rule: Cursor includes it only when @-mentioned.\"",
            "R015 FrontmatterFields no longer reports this case (was Warning, \"Rule never applies automatically\").",
        ]),
        new(1, Launch, "First versioned ruleset", [
            "Rules R001–R005, R007–R015. Scoring: Error 10, Warning 4, Info 1 per finding; at most 30 per rule; severity caps Error 40, Warning 30, Info 10.",
            "Before versioning (#1): R001–R005.",
            "Before versioning (#7): scoring and grades.",
            "Before versioning (#16): suppression comments; severity caps.",
            "Before versioning (#17): R002 and R005 precision fixes from the corpus run.",
            "Before versioning (#18): R007 HedgedInstructions, R008 EmphasisInflation, R009 SecretsInRulesFile, R012 UnclosedCodeFence.",
            "Before versioning (#21): R010 PersonaPreamble, R011 EmptySections, R013 ProseParagraph, R014 UnverifiableCrossReference.",
            "Before versioning (#22): format detection for Cursor, Copilot, Gemini and Windsurf files; R015 FrontmatterFields.",
            "Before versioning (#26): browser lint engine with the same results as the server.",
        ]),
    ];

    public static IReadOnlyList<ChangelogEntry> AppChecks { get; } =
    [
        new(null, new DateOnly(2026, 10, 3), "P014 skips symlinked rules files", ["A rules file whose tree mode is 120000 is not linted; the summary notes \"P014 skipped <path>: symlink to <target>.\" GitHub returns the target's content under the link's path, so findings were reported at the link with the target's line numbers.", "P005 and R009: values equal to pass, passwd, pwd, abc123, 123456, 12345678 or qwerty are treated as placeholders."]),
        new(null, Launch, "R006 MissingIdentifiers (#24)", ["App-only. Paths, package scripts, make targets and MSBuild targets named in rules files are checked against the repository."]),
        new(null, Launch, "P014 RulesFileLint (#23)", ["Lints rules files changed in the pull request with the lint rules. Severity follows each rule."]),
        new(null, Launch, "P008–P012 (#20)", ["Deleted tests, swallowed exceptions, new dependencies, CI config edited, debug leftovers."]),
        new(null, Launch, "P005–P007, P013 (#19)", ["Secrets in diff, skipped tests, suppressed diagnostics, committed artifacts."]),
        new(null, Launch, "P001–P004 (#8)", ["Placeholder identifiers, TODO stubs, assert-nothing tests (C#), out-of-scope files."]),
    ];
}
