using Aibysitter.Rules.Rules;

namespace Aibysitter.Rules.Tests;

public class VagueVerbsTests
{
    private readonly VagueVerbs _rule = new();

    [Fact]
    public void Fixture_ReportsExpectedLines()
    {
        var findings = _rule.Evaluate(Fixtures.Load("R002-vague-verbs.md")).ToList();

        Assert.Equal(3, findings.Count);
        Assert.Equal(new[] { 3, 5, 7 }, findings.Select(f => f.Line));
        Assert.All(findings, f => Assert.Equal("R002", f.RuleId));
    }

    [Fact]
    public void CleanFixture_ReportsNothing()
    {
        Assert.Empty(_rule.Evaluate(Fixtures.Load("clean.md")));
    }
}
