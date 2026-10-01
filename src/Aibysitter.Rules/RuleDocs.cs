namespace Aibysitter.Rules;

public static class RuleDocs
{
    public static IReadOnlyList<RuleDoc> All { get; } =
    [
        new(
            "R001",
            "RationaleProse",
            "Instruction lines explain why instead of stating what.",
            "\"because\", \"so that\", \"in order to\", \"the reason\", \"this ensures\", \"this helps\", \"which means\".",
            "- Use tabs because the formatter expects them.",
            "- Use tabs."),
        new(
            "R002",
            "VagueVerbs",
            "Instructions use verbs with no checkable outcome.",
            "\"handle\", \"manage\", \"deal with\", \"ensure\", \"improve\", \"optimize\", \"clean up\", \"properly\", \"appropriate(ly)\", \"as needed\".",
            "- Handle errors properly.",
            "- Return 400 with a ProblemDetails body when validation fails."),
        new(
            "R003",
            "ContradictoryModals",
            "The same instruction is both required and forbidden.",
            "A line starting with always / must and another starting with never / must not / do not, with the same remaining text.",
            "- Always use tabs.\n- Never use tabs.",
            "- Use tabs."),
        new(
            "R004",
            "FileLength",
            "The file is longer than 200 lines.",
            "Line count above 200. Reported once, at line 201.",
            "A 350-line CLAUDE.md covering every module.",
            "A root file under 200 lines; module rules in their own files."),
        new(
            "R005",
            "DuplicateLines",
            "The same line appears more than once.",
            "Repeated non-blank lines, ignoring case, whitespace, and list markers. Code blocks excluded.",
            "- Run tests before commit.\n* run tests before commit.",
            "- Run tests before commit."),
    ];

    private static readonly Dictionary<string, RuleDoc> ById = All.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

    public static RuleDoc? Find(string id) => ById.GetValueOrDefault(id);
}
