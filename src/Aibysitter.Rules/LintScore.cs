namespace Aibysitter.Rules;

/// <param name="DeductionsByRule">Per rule, after <see cref="Scorer.PerRuleCap"/>.</param>
/// <param name="DeductionsBySeverity">Per severity: rule total and the amount applied after <see cref="Scorer.SeverityCap"/>.</param>
public sealed record LintScore(
    int Value,
    string Grade,
    IReadOnlyDictionary<string, int> DeductionsByRule,
    IReadOnlyDictionary<Severity, SeverityDeduction> DeductionsBySeverity);

public sealed record SeverityDeduction(int Total, int Applied)
{
    public bool IsCapped => Applied < Total;
}
