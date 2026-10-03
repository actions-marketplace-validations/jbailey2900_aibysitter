namespace Aibysitter.Web.Linting;

/// <param name="Sample">Sampled findings judged.</param>
/// <param name="FalsePositivesA">False positives by judge A.</param>
/// <param name="FalsePositivesB">False positives by judge B.</param>
/// <param name="Ruleset">Ruleset version the rule was measured at.</param>
public sealed record RuleMeasurement(int Sample, int FalsePositivesA, int FalsePositivesB, int Ruleset);

/// <summary>
/// Measured false-positive rates per lint rule on the 755-file corpus (notes/field-note-3-data.md).
/// Review on every ruleset bump: <see cref="ReviewedAtRuleset"/> is pinned by a test against RulesetVersion.Current.
/// </summary>
public static class RuleMeasurements
{
    public const int ReviewedAtRuleset = 4;

    public const int CorpusFiles = 755;

    private static readonly Dictionary<string, RuleMeasurement> Measured = new(StringComparer.Ordinal)
    {
        ["R001"] = new(30, 14, 12, 4),
        ["R002"] = new(30, 11, 12, 4),
        ["R004"] = new(30, 0, 0, 2),
        ["R005"] = new(6, 1, 1, 4),
        ["R007"] = new(30, 4, 6, 4),
        ["R008"] = new(16, 1, 5, 4),
        ["R010"] = new(14, 0, 1, 2),
        ["R011"] = new(7, 0, 0, 4),
        ["R012"] = new(1, 0, 0, 2),
        ["R013"] = new(30, 4, 4, 4),
        ["R014"] = new(2, 0, 0, 2),
        ["R015"] = new(10, 0, 0, 2),
        ["R016"] = new(7, 0, 0, 2),
    };

    private static readonly Dictionary<string, string> NotMeasured = new(StringComparer.Ordinal)
    {
        ["R003"] = $"Not measured: no findings in the {CorpusFiles}-file corpus.",
        ["R006"] = "Not measured per finding (App-only; judged per file in field note 1).",
        ["R009"] = $"Not measured: no findings in the {CorpusFiles}-file corpus.",
    };

    public static RuleMeasurement? Find(string ruleId) => Measured.GetValueOrDefault(ruleId);

    public static string? NotMeasuredReason(string ruleId) => NotMeasured.GetValueOrDefault(ruleId);

    public static IEnumerable<string> RuleIds => Measured.Keys.Concat(NotMeasured.Keys).Order(StringComparer.Ordinal);

    /// <summary>"47% (14 of 30) and 40% (12 of 30), two judges, ruleset v4"; unchanged rules add "; rule unchanged since".</summary>
    public static string Describe(RuleMeasurement m) =>
        $"{Percent(m.FalsePositivesA, m.Sample)} ({m.FalsePositivesA} of {m.Sample}) and {Percent(m.FalsePositivesB, m.Sample)} ({m.FalsePositivesB} of {m.Sample}), two judges, ruleset v{m.Ruleset}"
        + (m.Ruleset < ReviewedAtRuleset ? "; rule unchanged since" : string.Empty);

    private static string Percent(int count, int total) =>
        $"{Math.Round(100.0 * count / total, MidpointRounding.AwayFromZero):0}%";
}
