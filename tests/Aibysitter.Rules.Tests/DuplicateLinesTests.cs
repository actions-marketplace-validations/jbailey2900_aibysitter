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

    [Theory]
    [InlineData("---")]
    [InlineData("* * *")]
    [InlineData("___")]
    [InlineData("| File | Reason |")]
    [InlineData("Examples:")]
    [InlineData("- Use tabs.")]
    public void Skips_RulesTablesAndShortLines(string line)
    {
        Assert.Empty(_rule.Evaluate(RulesFile.Parse($"{line}\n\ntext\n\n{line}\n")));
    }

    [Fact]
    public void Heading_RepeatedUnderDifferentParents_NotFlagged()
    {
        var text = "# Build\n## How to run it locally\n# Test\n## How to run it locally\n";

        Assert.Empty(_rule.Evaluate(RulesFile.Parse(text)));
    }

    [Fact]
    public void Heading_RepeatedUnderSameParent_Flagged()
    {
        var text = "# Build\n## How to run it locally\n## Other\n## How to run it locally\n";

        Assert.Equal(4, Assert.Single(_rule.Evaluate(RulesFile.Parse(text))).Line);
    }

    [Fact]
    public void Heading_DoesNotMatchBodyLineWithSameText()
    {
        var text = "## How to run it locally\nHow to run it locally\n";

        Assert.Empty(_rule.Evaluate(RulesFile.Parse(text)));
    }
}
