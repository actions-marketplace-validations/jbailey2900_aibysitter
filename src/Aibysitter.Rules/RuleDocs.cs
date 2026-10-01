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
            "\"handle\", \"manage\", \"deal with\", \"ensure\", \"improve\", \"optimize\", \"clean up\" opening an instruction, optionally after always / must / should / never / do not. \"properly\", \"appropriate(ly)\", \"as needed\" in an instruction. Skipped: table rows; inline code, links, paths, file names; a term followed by code, a path, a parenthesized list, \"e.g.\", three or more listed items, or a colon; \"ensure\" followed by a checkable statement; \"optimize for\"; \"where / when / if / as appropriate\".",
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
            "Repeated lines of four or more words, ignoring case, whitespace, and list markers. A heading repeats only under the same parent heading. Excluded: code blocks, horizontal rules, table rows.",
            "- Run tests before commit.\n* run tests before commit.",
            "- Run tests before commit."),
        new(
            "R007",
            "HedgedInstructions",
            "A hedge makes the instruction optional.",
            "\"try to\", \"if possible\", \"ideally\", \"where / when / whenever possible\", \"consider\", \"prefer to\", \"where / when / if / as appropriate\". Skipped: table rows; inline code, links, paths, quoted text; \"try to\" after not / never / don't; \"consider\" unless it opens a clause or follows you can / could / may / should; \"consider whether / if / how\"; clauses opening with a third-person or plural subject.",
            "- Try to keep functions short.",
            "- Keep functions under 40 lines."),
        new(
            "R008",
            "EmphasisInflation",
            "Emphasis is on so many lines that none of it stands out.",
            "Lines with IMPORTANT, CRITICAL, MUST, NEVER, ALWAYS, DO NOT, MANDATORY or REQUIRED in capitals, or \"!!\". Limit: 3 per 100 lines, minimum 3. Code blocks excluded. Reported once, at the first line over the limit.",
            "- ALWAYS run tests.\n- NEVER skip lint.\n- You MUST format.\n- IMPORTANT: commit often.",
            "- Run tests.\n- Run lint before commit.\n- NEVER push to main."),
        new(
            "R009",
            "SecretsInRulesFile",
            "The file contains a credential.",
            "Private key headers; AWS, GitHub, Slack, Anthropic, OpenAI, Stripe live and Google API keys; JSON Web Tokens; Password= and Pwd= values; credentials in URLs other than localhost. Code blocks included. Values with placeholder markers (xxxx, ..., <, >, {, }, $, *, your, example, changeme) are ignored. Findings show a redacted prefix only.",
            "- Connect with Server=db;User Id=app;Password=Hunter2Prod;",
            "- Read the connection string from the ConnectionStrings__Default environment variable."),
        new(
            "R010",
            "PersonaPreamble",
            "The file assigns the agent a persona instead of stating facts.",
            "\"You are / You're / Act as / Behave as / Pretend to be / Imagine you are\" followed by a, an, or the and expert, senior, principal, staff, lead, world-class, seasoned, experienced, veteran, elite, 10x, genius, master, guru, ninja, or rockstar within four words. Quoted text and inline code are skipped.",
            "You are a senior .NET engineer who writes clean code.",
            "This is a .NET 10 Razor Pages app. Run `dotnet test` before commit."),
        new(
            "R011",
            "EmptySections",
            "A heading has no content.",
            "A heading followed by a heading of the same or higher level, or end of file, with only blank lines or comments between. The file's first heading is exempt when it is level 1.",
            "# Rules\n## Testing\n## Style\n- Use tabs.",
            "# Rules\n## Testing\n- Run `dotnet test`.\n## Style\n- Use tabs."),
        new(
            "R012",
            "UnclosedCodeFence",
            "A code fence is opened and never closed.",
            "A ``` or ~~~ fence with no matching closing fence before end of file. Reported at the opening line. Every line after it is treated as code and skipped by other rules.",
            "Run:\n```\ndotnet test\n- Use tabs.",
            "Run:\n```\ndotnet test\n```\n- Use tabs."),
        new(
            "R013",
            "ProseParagraph",
            "Instructions are written as a long paragraph.",
            "A paragraph over 80 words. Paragraph: consecutive prose lines that are not list items, list-item continuation lines, table rows, or block quotes. Reported at the first line.",
            string.Join(" ", Enumerable.Repeat("Run the tests before you commit and keep the build green.", 9)),
            "- Run `dotnet test` before commit.\n- Keep the build green."),
        new(
            "R014",
            "UnverifiableCrossReference",
            "A line refers to something elsewhere without naming it.",
            "\"as mentioned / discussed / described / noted above, below, earlier, previously\", \"see above / below\", \"the usual way / approach / pattern / process / style\", \"the way we always / usually\", \"like before\". Not flagged when the line has a link, inline code, a quoted name, or a bold name.",
            "- Format dates the usual way.",
            "- Format dates as ISO 8601 (`yyyy-MM-dd`)."),
    ];

    private static readonly Dictionary<string, RuleDoc> ById = All.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

    public static RuleDoc? Find(string id) => ById.GetValueOrDefault(id);
}
