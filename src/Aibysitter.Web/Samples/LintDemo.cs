using Aibysitter.Rules;

namespace Aibysitter.Web.Samples;

public sealed record LintDemoLine(int Line, Severity Severity, string RuleId, string Message);

public sealed record LintDemoResult(string FileName, IReadOnlyList<LintDemoLine> Lines, LintScore Score);

/// <summary>Lints the embedded sample once; the result is cached for the process lifetime.</summary>
public sealed class LintDemo(LintEngine engine)
{
    public const string FileName = "CLAUDE.md";

    private readonly Lazy<LintDemoResult> result = new(() =>
    {
        var severities = engine.Rules.ToDictionary(r => r.Id, r => r.Severity, StringComparer.Ordinal);
        var findings = engine.Lint(SampleRules.Text);
        return new LintDemoResult(
            FileName,
            findings.Select(f => new LintDemoLine(f.Line, severities[f.RuleId], f.RuleId, f.Message)).ToList(),
            engine.Score(findings));
    });

    public LintDemoResult Result => result.Value;
}
