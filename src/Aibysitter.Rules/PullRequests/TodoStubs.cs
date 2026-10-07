using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

public sealed partial class TodoStubs : AddedLinePatternCheck
{
    public override string Id => "P002";
    public override string Title => "TODO stubs";
    public override Severity Severity => Severity.Warning;

    protected override Regex Pattern => StubRegex();
    protected override string MessagePrefix => "Stub left in change";
    protected override string FixHint => "Implement it, or remove it from this PR and track it in an issue.";

    protected override bool AppliesTo(string path) => FileKinds.IsCode(path);

    /// <summary>A match inside a string literal on its line is not a stub.</summary>
    protected override IEnumerable<string> Keep(ChangedFile file, DiffLine line, IReadOnlyList<string> matches)
    {
        var hashComments = CodeText.UsesHashComments(file.Path);
        return matches.Where(m => !CodeText.IsInsideStringLiteral(line.Text, line.Text.IndexOf(m, StringComparison.Ordinal), hashComments));
    }

    [GeneratedRegex(@"throw\s+new\s+NotImplementedException\s*\(|raise\s+NotImplementedError\b|\b(?:todo|unimplemented)!\s*\(|(?://|/\*|#|--)\s*(?:TODO|FIXME)\b")]
    private static partial Regex StubRegex();
}
