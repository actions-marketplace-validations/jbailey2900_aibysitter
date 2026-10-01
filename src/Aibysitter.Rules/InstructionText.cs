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
}
