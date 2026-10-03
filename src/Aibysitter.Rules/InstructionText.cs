using System.Text.RegularExpressions;

namespace Aibysitter.Rules;

/// <summary>Helpers for reading a prose line as instruction clauses.</summary>
internal static partial class InstructionText
{
    /// <summary>True for markdown table rows.</summary>
    public static bool IsTableRow(string text) => text.TrimStart().StartsWith('|');

    /// <summary>
    /// Line content with list marker, emphasis markers, and a leading <c>Label:</c> removed;
    /// inline code, link targets, URLs, and paths replaced by <c>␣CODE␣</c>.
    /// </summary>
    public static string Content(string text)
    {
        var s = CodeSpanRegex().Replace(text, " CODE ");
        s = LinkRegex().Replace(s, "$1");
        s = UrlRegex().Replace(s, " CODE ");
        s = PathRegex().Replace(s, " CODE ");
        s = FileNameRegex().Replace(s, " CODE ");
        s = ListMarkerRegex().Replace(s.Trim(), string.Empty);
        s = s.Replace("**", string.Empty).Replace("__", string.Empty);
        s = LabelRegex().Replace(s, string.Empty);
        return s.Trim();
    }

    /// <summary>Line text with inline code replaced by <c>␣CODE␣</c>; nothing else removed.</summary>
    public static string WithoutCode(string text) => CodeSpanRegex().Replace(text, " CODE ");

    /// <summary>Content split into sentence and clause units.</summary>
    public static IEnumerable<string> Clauses(string text) =>
        ClauseSplitRegex().Split(Content(text)).Select(c => c.Trim()).Where(c => c.Length > 0);

    /// <summary>
    /// True when the text after a matched term names a concrete target: inline code, a path, a parenthesized list,
    /// an "e.g." example, an enumeration of three or more items, or a trailing colon introducing a list.
    /// </summary>
    public static bool HasConcreteTarget(string afterTerm) =>
        afterTerm.Contains("CODE", StringComparison.Ordinal)
        || afterTerm.Contains('(')
        || afterTerm.Contains("e.g.", StringComparison.OrdinalIgnoreCase)
        || afterTerm.Count(c => c == ',') >= 2
        || afterTerm.TrimEnd().EndsWith(':');

    /// <summary>
    /// True when the sentence is an instruction: after <see cref="Content"/>, a clause opens with a directive word or an
    /// imperative verb (optionally after a leading "if / when / before … ," clause), or the sentence contains a modal.
    /// </summary>
    public static bool IsInstruction(string sentence) =>
        ModalRegex().IsMatch(Content(sentence)) || Clauses(sentence).Any(c => ImperativeStartRegex().IsMatch(c));

    /// <summary>Text split at sentence ends.</summary>
    public static IEnumerable<string> Sentences(string text) => SentenceSplitRegex().Split(text);

    /// <summary>True when the clause opens with a directive word or an imperative verb.</summary>
    public static bool StartsImperative(string clause) => ImperativeStartRegex().IsMatch(clause);

    /// <summary>
    /// Instruction units: a list item with its continuation lines, or a run of paragraph lines. Units end at blank lines,
    /// headings, code fences, frontmatter, suppression comments and table rows; a list marker starts a new unit.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<RulesLine>> Units(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var units = new List<IReadOnlyList<RulesLine>>();
        List<RulesLine>? current = null;
        foreach (var line in file.Lines)
        {
            if (!line.IsProse || IsTableRow(line.Text))
            {
                current = null;
                continue;
            }

            if (current is null || ListItemRegex().IsMatch(line.Text))
            {
                current = [];
                units.Add(current);
            }

            current.Add(line);
        }

        return units;
    }

    /// <summary>A unit's lines trimmed and joined with one space, with each line's start offset in the joined text.</summary>
    public static (string Text, IReadOnlyDictionary<RulesLine, int> Starts) Join(IReadOnlyList<RulesLine> unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        var text = new System.Text.StringBuilder();
        var starts = new Dictionary<RulesLine, int>(ReferenceEqualityComparer.Instance);
        foreach (var line in unit)
        {
            if (text.Length > 0)
            {
                text.Append(' ');
            }

            starts[line] = text.Length - (line.Text.Length - line.Text.TrimStart().Length);
            text.Append(line.Text.Trim());
        }

        return (text.ToString(), starts);
    }

    /// <summary>Sentence spans of the text: start index and length, in order.</summary>
    public static IReadOnlyList<(int Start, int Length)> SentenceSpans(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var spans = new List<(int, int)>();
        var start = 0;
        foreach (Match boundary in SentenceSplitRegex().Matches(text))
        {
            spans.Add((start, boundary.Index - start));
            start = boundary.Index + boundary.Length;
        }

        spans.Add((start, text.Length - start));
        return spans;
    }

    /// <summary>True for list-item lines.</summary>
    public static bool IsListItem(string text) => ListItemRegex().IsMatch(text);

    /// <summary>True for a line that is one HTML comment.</summary>
    public static bool IsHtmlComment(string text) => HtmlCommentRegex().IsMatch(text);

    [GeneratedRegex(@"`[^`]*`")]
    private static partial Regex CodeSpanRegex();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"(?<![\w])[\w.-]*/[\w./-]+")]
    private static partial Regex PathRegex();

    [GeneratedRegex(@"\b[\w-]+\.(?:md|mdc|json|ya?ml|toml|txt|cs|csproj|ts|tsx|js|jsx|py|go|rs|rb|sh|ps1|sql|xml|html|css)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FileNameRegex();

    [GeneratedRegex(@"^(?:[-*+]|\d+[.)])\s+(?:\[[ xX]\]\s+)?")]
    private static partial Regex ListMarkerRegex();

    [GeneratedRegex(@"^[^:.;!?]{1,40}:\s+")]
    private static partial Regex LabelRegex();

    [GeneratedRegex(@"(?<=[.;!?])\s+|\s+(?:—|–|-)\s+")]
    private static partial Regex ClauseSplitRegex();

    /// <summary>Sentence ends: ".", "!", "?" or ";" followed by whitespace, not after "e.g.", "i.e.", "etc." or "vs.".</summary>
    [GeneratedRegex(@"(?<!\b(?:e\.g|i\.e|etc|vs)\.)(?<=[.;!?])\s+", RegexOptions.IgnoreCase)]
    private static partial Regex SentenceSplitRegex();

    [GeneratedRegex(@"^\s*(?:[-*+]|\d+[.)])\s")]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"^\s*<!--.*-->\s*$")]
    private static partial Regex HtmlCommentRegex();

    /// <summary>Modal or obligation anywhere in the sentence.</summary>
    [GeneratedRegex(@"\b(?:must|mustn't|should|shouldn't|shall|do\s+not|don't|never|needs?\s+to|ha(?:ve|s)\s+to|(?:is|are)\s+required)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ModalRegex();

    /// <summary>
    /// Clause opens, optionally after one leading "if / when / before … ," clause, with a directive word or a common imperative verb.
    /// </summary>
    [GeneratedRegex(@"^(?:(?:if|when|whenever|before|after|unless|once|while|for)\b[^,]{0,80},\s*)?(?:(?:always|must|should|never|please|only|do\s+not|don't|make\s+sure)\b|(?:use|add|run|create|check|keep|avoid|follow|write|call|set|make|prefer|return|update|verify|validate|select|export|edit|assign|move|organize|configure|cache|continue|read|merge|adapt|wrap|log|put|place|define|store|import|include|apply|choose|pick|install|build|deploy|commit|review|refactor|implement|handle|manage|ensure|treat|throw|catch|raise|split|sort|mark|limit|scale|tune|register|inject|convert|escape|sanitize|encode|close|dispose|release|retry|notify|load|save|fetch|send|clean|remove|delete|replace|rename|extend|override|reuse|share|enable|disable|initialize|init|stop|do|be|try|ask|test|document|let|wait|yield|report|respond|reply|explain|mention|start|look|find|search|prefix|name|format|lint|push|open|ignore|skip|leave|give|provide|generate|output|print|show|list|state|note|remember|consider|think|plan|confirm|comment|declare|separate|group|order|focus|stick|target|match|mirror|copy|pass|prompt|tell|summarize|describe|reference|link|cite|flag|emit|exit|abort|fail|drop|batch|lock|pin|bump|tag|label|track|measure|profile|benchmark|monitor|watch|parse|serialize|render|display|answer|clarify|request|query|refer|see|assume|go|get)\b)", RegexOptions.IgnoreCase)]
    private static partial Regex ImperativeStartRegex();
}
