using Aibysitter.Rules.Rules;

namespace Aibysitter.Rules.Tests;

public class FileLengthTests
{
    private readonly FileLength _rule = new();

    [Fact]
    public void Fixture_ReportsExpectedLines()
    {
        var findings = _rule.Evaluate(Fixtures.Load("R004-file-length-over.md")).ToList();

        Assert.Single(findings);
        Assert.Equal(new[] { 201 }, findings.Select(f => f.Line));
        Assert.All(findings, f => Assert.Equal("R004", f.RuleId));
    }

    [Fact]
    public void FileAtLimit_ReportsNothing()
    {
        Assert.Empty(_rule.Evaluate(Fixtures.Load("R004-file-length-at-limit.md")));
    }

    [Fact]
    public void CleanFixture_ReportsNothing()
    {
        Assert.Empty(_rule.Evaluate(Fixtures.Load("clean.md")));
    }
}
