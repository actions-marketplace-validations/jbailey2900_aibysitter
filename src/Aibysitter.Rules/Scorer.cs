namespace Aibysitter.Rules;

public static class Scorer
{
    public const int MaxScore = 100;
    public const int PerRuleCap = 30;

    public static int Weight(Severity severity) => severity switch
    {
        Severity.Error => 10,
        Severity.Warning => 4,
        Severity.Info => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
    };

    public static string Grade(int score) => score switch
    {
        >= 90 => "A",
        >= 80 => "B",
        >= 70 => "C",
        >= 60 => "D",
        _ => "F",
    };

    public static LintScore Score(IEnumerable<Finding> findings, IReadOnlyDictionary<string, Severity> severityByRule)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(severityByRule);

        var deductions = findings
            .GroupBy(f => f.RuleId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => Math.Min(PerRuleCap, g.Count() * Weight(severityByRule[g.Key])),
                StringComparer.Ordinal);

        var value = Math.Max(0, MaxScore - deductions.Values.Sum());
        return new LintScore(value, Grade(value), deductions);
    }
}
