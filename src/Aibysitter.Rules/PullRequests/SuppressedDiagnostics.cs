using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

public sealed partial class SuppressedDiagnostics : AddedLinePatternCheck
{
    public override string Id => "P007";
    public override string Title => "Suppressed diagnostics";
    public override Severity Severity => Severity.Warning;

    protected override Regex Pattern => SuppressRegex();
    protected override string MessagePrefix => "Diagnostic suppressed";
    protected override string FixHint => "Fix the warning. If the suppression stays, scope it to one line and state the reason.";

    protected override bool AppliesTo(string path) =>
        FileKinds.IsCodeOrConfig(path) || Path.GetFileName(path).Equals(".editorconfig", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// C# #pragma warning disable, [SuppressMessage], ReSharper disable, &lt;NoWarn&gt;, .editorconfig severity = none;
    /// eslint-disable, @ts-ignore, @ts-nocheck; noqa, type: ignore, pylint: disable, pyright: ignore;
    /// Go nolint; Rust #[allow]; Java @SuppressWarnings.
    /// </summary>
    [GeneratedRegex(@"#pragma\s+warning\s+disable\b|\[\s*(?:assembly:\s*)?SuppressMessage\s*\(|ReSharper\s+disable\b|<NoWarn>|dotnet_diagnostic\.\w+\.severity\s*=\s*none\b|eslint-disable\b|@ts-(?:ignore|nocheck)\b|#\s*noqa\b|#\s*type:\s*ignore\b|#\s*pylint:\s*disable\b|#\s*pyright:\s*ignore\b|//\s*nolint\b|#!?\[\s*allow\s*\(|@SuppressWarnings\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex SuppressRegex();
}
