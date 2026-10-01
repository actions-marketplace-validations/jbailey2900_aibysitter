namespace Aibysitter.Rules;

public sealed record LintScore(int Value, string Grade, IReadOnlyDictionary<string, int> DeductionsByRule);
