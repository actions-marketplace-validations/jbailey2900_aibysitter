using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

/// <summary>
/// Added antiforgery and authorization exemptions in ASP.NET Core code outside test files: [IgnoreAntiforgeryToken],
/// [AllowAnonymous], .DisableAntiforgery(), .AllowAnonymous(). Comment lines and string literals are skipped.
/// </summary>
public sealed partial class SecurityExemptions : AddedLinePatternCheck
{
    public override string Id => "P016";
    public override string Title => "Security exemptions";
    public override Severity Severity => Severity.Warning;

    protected override Regex Pattern => ExemptionRegex();
    protected override string MessagePrefix => "Security exemption added";
    protected override string FixHint => "Confirm the endpoint needs the exemption and that nothing else relies on the protection.";

    protected override bool AppliesTo(string path) =>
        !FileKinds.IsTestFile(path) && Path.GetExtension(path).ToLowerInvariant() is ".cs" or ".cshtml" or ".razor";

    protected override IEnumerable<string> Keep(ChangedFile file, DiffLine line, IReadOnlyList<string> matches) =>
        CodeText.IsCommentOnly(line.Text)
            ? []
            : matches.Where(m => !CodeText.IsInsideStringLiteral(line.Text, line.Text.IndexOf(m, StringComparison.Ordinal)));

    [GeneratedRegex(@"\[\s*(?:IgnoreAntiforgeryToken|AllowAnonymous)\b|\.\s*(?:DisableAntiforgery|AllowAnonymous)\s*\(\s*\)")]
    private static partial Regex ExemptionRegex();
}
