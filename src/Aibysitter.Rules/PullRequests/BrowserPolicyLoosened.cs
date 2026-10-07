using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

/// <summary>
/// Added Content-Security-Policy and CORS relaxations in code and config files outside test files: 'unsafe-inline',
/// 'unsafe-eval', AllowAnyOrigin(), and Access-Control-Allow-Origin set to *. Comment lines are skipped.
/// </summary>
public sealed partial class BrowserPolicyLoosened : AddedLinePatternCheck
{
    public override string Id => "P018";
    public override string Title => "Browser policy loosened";
    public override Severity Severity => Severity.Warning;

    protected override Regex Pattern => LoosenedRegex();
    protected override string MessagePrefix => "Browser security policy loosened";
    protected override string FixHint => "Use nonces or hashes instead of 'unsafe-inline' or 'unsafe-eval', and list allowed origins instead of any origin.";

    protected override bool AppliesTo(string path) => FileKinds.IsCodeOrConfig(path) && !FileKinds.IsTestFile(path);

    protected override IEnumerable<string> Keep(ChangedFile file, DiffLine line, IReadOnlyList<string> matches) =>
        CodeText.IsCommentOnly(line.Text) ? [] : matches;

    [GeneratedRegex(@"'unsafe-(?:inline|eval)'|\bAllowAnyOrigin\s*\(\s*\)|Access-Control-Allow-Origin[""'\]]*\s*[,:=]\s*[""']?\*", RegexOptions.IgnoreCase)]
    private static partial Regex LoosenedRegex();
}
