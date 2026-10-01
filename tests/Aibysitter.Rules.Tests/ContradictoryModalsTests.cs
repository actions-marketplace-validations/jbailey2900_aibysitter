using Aibysitter.Rules.Rules;

namespace Aibysitter.Rules.Tests;

public class ContradictoryModalsTests
{
    private readonly ContradictoryModals _rule = new();

    [Fact]
    public void Fixture_ReportsExpectedLines()
    {
        var findings = _rule.Evaluate(Fixtures.Load("R003-contradictory-modals.md")).ToList();

        Assert.Equal(2, findings.Count);
        Assert.Equal(new[] { 8, 9 }, findings.Select(f => f.Line));
        Assert.All(findings, f => Assert.Equal("R003", f.RuleId));
    }

    [Fact]
    public void CleanFixture_ReportsNothing()
    {
        Assert.Empty(_rule.Evaluate(Fixtures.Load("clean.md")));
    }
}
