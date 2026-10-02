namespace Aibysitter.Rules;

public sealed record ReportFinding(string Rule, int Line, string Severity, string Message, string FixHint);

/// <summary>Lint result as returned by <c>/api/lint</c> and <c>aibysitter lint --json</c>.</summary>
public sealed record LintReport(
    int RulesetVersion,
    string DetectedFormat,
    int Score,
    string Grade,
    IReadOnlyList<string> Disabled,
    IReadOnlyList<ReportFinding> Findings,
    IReadOnlyList<ReportFinding> Suppressed)
{
    /// <summary>Upper-cased, de-duplicated, ordered rule IDs. False with the offending IDs when any is not a rule of <paramref name="engine"/>.</summary>
    public static bool TryNormalizeDisabled(LintEngine engine, IEnumerable<string?>? ids, out IReadOnlyList<string> disabled, out IReadOnlyList<string> unknown)
    {
        var requested = (ids ?? []).Select(id => (id ?? string.Empty).Trim().ToUpperInvariant()).Distinct().ToList();
        unknown = requested.Where(id => !engine.Rules.Any(r => r.Id == id)).ToList();
        disabled = unknown.Count == 0 ? requested.Order(StringComparer.Ordinal).ToList() : [];
        return unknown.Count == 0;
    }

    /// <param name="disabled">Normalized IDs from <see cref="TryNormalizeDisabled"/>.</param>
    public static LintReport Create(LintEngine engine, string text, RulesFormat format, IReadOnlyList<string> disabled)
    {
        var active = engine.Without(disabled);
        var result = active.Analyze(text, format);
        return From(result, active.Score(result.Findings), disabled, active.SeverityOf);
    }

    public static LintReport From(LintResult result, LintScore score, IReadOnlyList<string> disabled, Func<string, Severity> severityOf) => new(
        global::Aibysitter.Rules.RulesetVersion.Current,
        result.Format.ToString(),
        score.Value,
        score.Grade,
        disabled,
        result.Findings.Select(f => ToReport(f, severityOf)).ToList(),
        result.Suppressed.Select(f => ToReport(f, severityOf)).ToList());

    private static ReportFinding ToReport(Finding finding, Func<string, Severity> severityOf) =>
        new(finding.RuleId, finding.Line, severityOf(finding.RuleId).ToString(), finding.Message, finding.FixHint);
}
