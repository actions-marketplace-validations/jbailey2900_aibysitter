namespace Aibysitter.Rules;

/// <param name="Format">Resolved format of the linted file.</param>
public sealed record LintResult(IReadOnlyList<Finding> Findings, IReadOnlyList<Finding> Suppressed, RulesFormat Format = RulesFormat.Markdown);
