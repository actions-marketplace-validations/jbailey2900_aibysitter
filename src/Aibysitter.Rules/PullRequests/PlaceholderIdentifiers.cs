using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

public sealed partial class PlaceholderIdentifiers : AddedLinePatternCheck
{
    public override string Id => "P001";
    public override string Title => "Placeholder identifiers";
    public override Severity Severity => Severity.Error;

    protected override Regex Pattern => PlaceholderRegex();
    protected override string MessagePrefix => "Placeholder left in change";
    protected override string FixHint => "Replace with the real identifier or value.";

    /// <summary>Code and config files outside test files.</summary>
    protected override bool AppliesTo(string path) => FileKinds.IsCodeOrConfig(path) && !FileKinds.IsTestFile(path);

    /// <summary>
    /// Not findings: comment lines (including inside block comments and docstrings), test-data attributes
    /// ([InlineData], [TestCase], [DataRow]), and placeholders that a comment line in the same file names (a documented template).
    /// </summary>
    protected override IEnumerable<string> Keep(ChangedFile file, DiffLine line, IReadOnlyList<string> matches)
    {
        var (commentLines, documented) = Comments(file);
        return commentLines.Contains(line.NewLine!.Value) || TestDataRegex().IsMatch(line.Text)
            ? []
            : matches.Where(m => !documented.Contains(m));
    }

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<ChangedFile, Tuple<IReadOnlySet<int>, HashSet<string>>> comments = new();

    /// <summary>Comment line numbers, and the placeholders those lines name.</summary>
    private (IReadOnlySet<int> Lines, HashSet<string> Documented) Comments(ChangedFile file)
    {
        var entry = comments.GetValue(file, f =>
        {
            var lines = CodeText.CommentLines(f);
            var texts = f.HeadContent is { } head
                ? head.Replace("\r\n", "\n").Split('\n').Where((_, i) => lines.Contains(i + 1))
                : f.Lines.Where(l => l.NewLine is { } n && lines.Contains(n)).Select(l => l.Text);
            var documented = texts.SelectMany(t => PlaceholderRegex().Matches(t).Select(m => m.Value)).ToHashSet(StringComparer.Ordinal);
            return Tuple.Create(lines, documented);
        });
        return (entry.Item1, entry.Item2);
    }

    [GeneratedRegex(@"^\s*\[\s*(?:[\w.]+(?:\([^)]*\))?\s*,\s*)*(?:InlineData|TestCase|DataRow)(?:Attribute)?\s*\(")]
    private static partial Regex TestDataRegex();

    [GeneratedRegex(@"::[A-Z][A-Z0-9_]*::|\bREPLACE_ME\b|\bYOUR_[A-Z0-9_]+\b|(?i:<placeholder>|<your[-_ ][^>]+>)")]
    private static partial Regex PlaceholderRegex();
}
