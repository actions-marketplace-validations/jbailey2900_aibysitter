using Aibysitter.Rules.Rules;

namespace Aibysitter.Rules.Tests;

public class RationaleProseTests
{
    private readonly RationaleProse _rule = new();

    [Fact]
    public void Fixture_ReportsExpectedLines()
    {
        var findings = _rule.Evaluate(Fixtures.Load("R001-rationale-prose.md")).ToList();

        Assert.Equal(2, findings.Count);
        Assert.Equal(new[] { 3, 5 }, findings.Select(f => f.Line));
        Assert.All(findings, f => Assert.Equal("R001", f.RuleId));
    }

    [Fact]
    public void CleanFixture_ReportsNothing()
    {
        Assert.Empty(_rule.Evaluate(Fixtures.Load("clean.md")));
    }
}
