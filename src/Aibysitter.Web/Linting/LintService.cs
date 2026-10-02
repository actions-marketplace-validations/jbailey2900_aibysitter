using Aibysitter.Rules;

namespace Aibysitter.Web.Linting;

public sealed record LintRow(Finding Finding, string Title, Severity Severity);

public sealed record LintOutcome(
    RulesFormat Format,
    IReadOnlyList<LintRow> Findings,
    IReadOnlyList<LintRow> Suppressed,
    LintScore Score,
    IReadOnlyList<string> Disabled,
    LintReport Report);

/// <summary>Server-side lint for the form and the API: optional disabled rules, scoring, and one log line without the text.</summary>
public sealed class LintService(LintEngine engine, ILogger<LintService> logger)
{
    public IReadOnlyList<IRule> Rules => engine.Rules;

    /// <summary>See <see cref="LintReport.TryNormalizeDisabled"/>.</summary>
    public bool TryNormalizeDisabled(IEnumerable<string?>? ids, out IReadOnlyList<string> disabled, out IReadOnlyList<string> unknown) =>
        LintReport.TryNormalizeDisabled(engine, ids, out disabled, out unknown);

    /// <param name="disabled">Normalized IDs from <see cref="TryNormalizeDisabled"/>.</param>
    /// <param name="source">"form" or "api", for the log line.</param>
    public LintOutcome Lint(string text, RulesFormat format, IReadOnlyList<string> disabled, string source)
    {
        var active = engine.Without(disabled);
        var rules = active.Rules.ToDictionary(r => r.Id);
        var result = active.Analyze(text, format);
        var score = active.Score(result.Findings);
        var outcome = new LintOutcome(
            result.Format,
            result.Findings.Select(f => Row(f, rules)).ToList(),
            result.Suppressed.Select(f => Row(f, rules)).ToList(),
            score,
            disabled,
            LintReport.From(result, score, disabled, active.SeverityOf));

        logger.LogInformation(
            "Linted {Length} chars as {Format} ({Source}), {FindingCount} findings, {SuppressedCount} suppressed, {DisabledCount} rules off, score {Score}",
            text.Length, outcome.Format, source, outcome.Findings.Count, outcome.Suppressed.Count, disabled.Count, outcome.Score.Value);
        return outcome;
    }

    private static LintRow Row(Finding finding, Dictionary<string, IRule> rules) =>
        new(finding, rules[finding.RuleId].Title, rules[finding.RuleId].Severity);
}
