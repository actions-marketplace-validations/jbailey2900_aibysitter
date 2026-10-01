using Aibysitter.Rules.Rules;

namespace Aibysitter.Rules.Tests;

public class DuplicateLinesTests
{
    private readonly DuplicateLines _rule = new();

    [Fact]
    public void Fixture_ReportsExpectedLines()
    {
        var findings = _rule.Evaluate(Fixtures.Load("R005-duplicate-lines.md")).ToList();

        Assert.Equal(2, findings.Count);
        Assert.Equal(new[] { 5, 10 }, findings.Select(f => f.Line));
        Assert.All(findings, f => Assert.Equal("R005", f.RuleId));
    }

    [Fact]
    public void CleanFixture_ReportsNothing()
    {
        Assert.Empty(_rule.Evaluate(Fixtures.Load("clean.md")));
    }
}
