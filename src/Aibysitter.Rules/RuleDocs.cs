namespace Aibysitter.Rules;

public static class RuleDocs
{
    public static IReadOnlyList<RuleDoc> All { get; } =
    [
        new(
            "R001",
            "RationaleProse",
            "Instructions explain why instead of stating what.",
            "\"because\", \"so that\", \"in order to\", \"the reason\", \"this ensures\", \"this helps\", \"which means\" in an instruction: a list item that contains an instruction sentence, or a paragraph sentence that is an instruction and the sentence after it. Instruction sentence: a clause opens with a directive word or an imperative verb (optionally after \"if / when / where / ideally … ,\"); or a label opens with one (\"Be concise:\"); or it contains must, should, shall, do not, never, need to, have to, is required, you can / could / may / might; or it is a list item of at most 12 words with no label, no verb form such as is / are / has / uses, and no leading determiner or pronoun (\"- One concept per file.\"). Skipped: descriptive prose; HTML comments; quoted text; phrases after \"/\" or a quote mark; \"just / only / simply / merely because\"; \"because of\"; \"the reason\" as a bare object.",
            "- Use tabs because the formatter expects them.",
            "- Use tabs."),
        new(
            "R002",
            "VagueVerbs",
            "Pattern-based: instructions use verbs with no checkable outcome.",
            "\"handle\", \"manage\", \"deal with\", \"ensure\", \"improve\", \"optimize\", \"clean up\" opening an instruction, optionally after always / must / should / never / do not. \"properly\", \"appropriate(ly)\", \"as needed\" in an instruction. Instruction lines only (instruction sentence as in R001). Skipped: table rows; inline code, links, paths, file names; a term followed by code, a path, a parenthesized list, \"e.g.\", three or more listed items, or a colon; \"clean up / handle / manage\" followed by files, folders, directories, branches, containers, processes, worktrees, temp data, volumes, subscriptions, listeners or timers; \"ensure\" after inline code on the line, or followed by a checkable statement; a qualifier in an \"ensure\" clause after inline code, followed by a gerund (\"properly functioning\") or a purpose or condition (\"as needed for …\"), or after more / most / less / least; \"optimize for\"; \"where / when / if / as appropriate\".",
            "- Handle errors properly.",
            "- Return 400 with a ProblemDetails body when validation fails."),
        new(
            "R003",
            "ContradictoryModals",
            "Mirrored instructions: the same instruction is both required and forbidden.",
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
            "The same instruction appears more than once.",
            "Repeated instruction lines (instruction sentence as in R001) of four or more words, ignoring case, whitespace, and list markers. Excluded: headings, code blocks, indented code, horizontal rules, table rows, HTML comments, wrapped continuation lines. Not reported: a line that occurs three or more times (template), or a repeat whose previous or next line also repeats the line beside the first occurrence (repeated block, such as parallel procedures).",
            "- Run tests before commit.\n* run tests before commit.",
            "- Run tests before commit."),
        new(
            "R006",
            "MissingIdentifiers",
            "A rules file names a path, script, or target that does not exist in the repository.",
            "App-only, through P014. Paths: inline code, link targets, and bare relative paths with a file extension; two or more segments, first segment present beside the rules file or at the root. Commands: npm/pnpm/yarn/bun run scripts (and npm test, and names with ':'), make targets, MSBuild -t: targets, dotnet project paths. Skipped: gitignored paths, generated folders, placeholders, example names, export subpaths under a package folder, lines that describe a file as an example, to create, forbidden, removed, or generated, and scripts in workspace repos not found beside the rules file or at the root. Changed rules files: every reference. Unchanged rules files: only references broken by paths or scripts the pull request removes or renames.",
            "- Run `npm run check-version` before deploying.\n- Hooks live in `src/hooks/`.",
            "- Run `npm run lint` before committing.\n- Hooks live in `src/lib/hooks/`.",
            AppOnly: true),
        new(
            "R007",
            "HedgedInstructions",
            "A hedge makes the instruction optional.",
            "\"try to\", \"if possible\", \"ideally\", \"where / when / whenever possible\", \"consider\", \"prefer to\", \"where / when / if / as appropriate\". Instruction lines only (instruction sentence as in R001). Skipped: table rows; inline code, links, paths, quoted text; \"try to\" after not / never / don't / can / could / may / might; \"consider\" unless it opens a clause or follows you can / could / may / should; \"consider whether / if / how\"; \"consider\" followed by =, : or / (a label); clauses opening with a third-person or plural subject.",
            "- Try to keep functions short.",
            "- Keep functions under 40 lines."),
        new(
            "R008",
            "EmphasisInflation",
            "Emphasis is on so many lines that none of it stands out.",
            "Lines with IMPORTANT, CRITICAL, MUST, NEVER, ALWAYS, DO NOT, MANDATORY or REQUIRED in capitals, or \"!!\". In a file with a capitalized SHOULD or a mention of RFC 2119 or BCP 14, MUST and REQUIRED are keywords and not counted. Limit: 3 per 100 lines, minimum 3. Code blocks excluded. Reported once, at the first line over the limit.",
            "- ALWAYS run tests.\n- NEVER skip lint.\n- You MUST format.\n- IMPORTANT: commit often.",
            "- Run tests.\n- Run lint before commit.\n- NEVER push to main."),
        new(
            "R009",
            "SecretsInRulesFile",
            "The file contains a credential.",
            "Private key headers; AWS, GitHub, Slack, Anthropic, OpenAI, Stripe live and Google API keys; JSON Web Tokens; Password= and Pwd= values; credentials in URLs other than localhost. Code blocks included. Values with placeholder markers (xxxx, ..., <, >, {, }, $, *, your, example, changeme) and the example passwords pass, passwd, pwd, abc123, 123456, 12345678, qwerty are ignored. Findings show a redacted prefix only.",
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
            "A heading followed by a heading of the same or higher level, or end of file, with only blank lines or comments between. Content: a deeper heading; a level-1 heading after it whose section has content (wrapper for an embedded document). Exempt: the file's first heading when it is level 1; a heading that is an instruction of four or more words or contains inline code; headings after a line ending in \":\" (examples); in .cursorrules and .windsurfrules, adjacent heading lines of the same level (comment block).",
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
            "A paragraph over 80 words that contains an instruction sentence (as in R001). Paragraph: consecutive prose lines that are not list items, list-item continuation lines, table rows, or block quotes. Descriptive paragraphs are not flagged. Reported at the first line.",
            string.Join(" ", Enumerable.Repeat("Run the tests before you commit and keep the build green.", 9)),
            "- Run `dotnet test` before commit.\n- Keep the build green."),
        new(
            "R014",
            "UnverifiableCrossReference",
            "A line refers to something elsewhere without naming it.",
            "\"as mentioned / discussed / described / noted above, below, earlier, previously\", \"see above / below\", \"the usual way / approach / pattern / process / style\", \"the way we always / usually\", \"like before\". Not flagged when the line has a link, inline code, a quoted name, or a bold name.",
            "- Format dates the usual way.",
            "- Format dates as ISO 8601 (`yyyy-MM-dd`)."),
        new(
            "R015",
            "FrontmatterFields",
            "A Cursor rule's frontmatter is missing, has unknown keys, or has an invalid alwaysApply.",
            "Cursor .mdc rules only: no frontmatter; keys other than description, globs, alwaysApply; alwaysApply not true or false. Manual-mode rules are R016.",
            "---\ndescriptoin: Formatting rules for C# files\nglobs: **/*.cs\nalwaysApply: maybe\n---\n- Use tabs.",
            "---\ndescription: Formatting rules for C# files\nglobs: **/*.cs\nalwaysApply: false\n---\n- Use tabs."),
        new(
            "R016",
            "ManualCursorRule",
            "A Cursor rule is in Apply Manually mode and is included only when @-mentioned.",
            "Cursor .mdc rules only: frontmatter present, alwaysApply not true, globs empty, description empty.",
            "---\ndescription:\nglobs:\nalwaysApply: false\n---\n- Use tabs.",
            "---\ndescription: Formatting rules for C# files\nglobs: **/*.cs\nalwaysApply: false\n---\n- Use tabs."),
    ];

    private static readonly Dictionary<string, RuleDoc> ById = All.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

    public static RuleDoc? Find(string id) => ById.GetValueOrDefault(id);
}
