namespace Aibysitter.Rules;

/// <param name="Version">Ruleset version; null for App-check entries, which do not bump it.</param>
public sealed record ChangelogEntry(int? Version, DateOnly Date, string Title, IReadOnlyList<string> Changes);

/// <summary>What changed in each ruleset version, and in the App's checks. Newest first.</summary>
public static class RulesetChangelog
{
    private static readonly DateOnly Launch = new(2026, 10, 1);

    public static IReadOnlyList<ChangelogEntry> Ruleset { get; } =
    [
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
