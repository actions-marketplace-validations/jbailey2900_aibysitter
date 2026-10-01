namespace Aibysitter.Rules;

public sealed record LintResult(IReadOnlyList<Finding> Findings, IReadOnlyList<Finding> Suppressed);
