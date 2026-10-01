namespace Aibysitter.Rules.Tests;

public class ScorerTests
{
    private static readonly Dictionary<string, Severity> Severities = new()
    {
        ["E"] = Severity.Error,
        ["W"] = Severity.Warning,
        ["I"] = Severity.Info,
    };

    private static Finding[] Many(string ruleId, int count) =>
        Enumerable.Range(1, count).Select(n => new Finding(ruleId, n, "m", "h")).ToArray();

    [Fact]
    public void NoFindings_Scores100_A()
    {
        var score = Scorer.Score([], Severities);

        Assert.Equal(100, score.Value);
        Assert.Equal("A", score.Grade);
        Assert.Empty(score.DeductionsByRule);
    }

    [Theory]
    [InlineData("E", 90)]
    [InlineData("W", 96)]
    [InlineData("I", 99)]
    public void OneFinding_DeductsSeverityWeight(string ruleId, int expected)
    {
        Assert.Equal(expected, Scorer.Score(Many(ruleId, 1), Severities).Value);
    }

    [Fact]
    public void DeductionsPerRule_AreCappedAt30()
    {
        var score = Scorer.Score(Many("W", 20), Severities);

        Assert.Equal(30, score.DeductionsByRule["W"]);
        Assert.Equal(70, score.Value);
    }

    [Fact]
    public void Deductions_SumAcrossRules()
    {
        var score = Scorer.Score([.. Many("E", 2), .. Many("W", 3), .. Many("I", 4)], Severities);

        Assert.Equal(new Dictionary<string, int> { ["E"] = 20, ["I"] = 4, ["W"] = 12 }, score.DeductionsByRule);
        Assert.Equal(64, score.Value);
    }

    [Fact]
    public void Score_FloorsAtZero()
    {
        var score = Scorer.Score([.. Many("E", 5), .. Many("W", 10), .. Many("I", 40), .. Many("X", 5)],
            new Dictionary<string, Severity>(Severities) { ["X"] = Severity.Error });

        Assert.Equal(0, score.Value);
        Assert.Equal("F", score.Grade);
    }

    [Theory]
    [InlineData(100, "A")]
    [InlineData(90, "A")]
    [InlineData(89, "B")]
    [InlineData(80, "B")]
    [InlineData(79, "C")]
    [InlineData(70, "C")]
    [InlineData(69, "D")]
    [InlineData(60, "D")]
    [InlineData(59, "F")]
    [InlineData(0, "F")]
    public void Grade_Boundaries(int score, string grade)
    {
        Assert.Equal(grade, Scorer.Grade(score));
    }

    [Fact]
    public void Engine_ScoresWithItsRuleSeverities()
    {
        var engine = new LintEngine();
        var findings = engine.Lint(Fixtures.Load("R003-contradictory-modals.md"));

        var score = engine.Score(findings);

        Assert.Equal(20, score.DeductionsByRule["R003"]);
        Assert.Equal(80, score.Value);
        Assert.Equal("B", score.Grade);
    }
}
