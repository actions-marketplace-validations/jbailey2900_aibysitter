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
    public void InstructionScope_FlagsInstructionsOnly()
    {
        var findings = _rule.Evaluate(Fixtures.Load("v3/R001-instruction-scope.md")).ToList();

        Assert.Equal(new[] { 3, 4, 11, 13, 19, 22 }, findings.Select(f => f.Line));
        Assert.Equal("Rationale prose: \"this ensures\"", findings[^2].Message);
    }

    [Theory]
    [InlineData("- This helps quickly identify which tasks are blocking work.")]
    [InlineData("- `compileDebugKotlin` fails because the patch adds new props.")]
    [InlineData("In this tutorial, you will extend the app so that it can read files.")]
    [InlineData("Real SIM cards pass these checks because they look like consumer numbers.")]
    [InlineData("Your knowledge is out of date because your training date is in the past.")]
    public void DescriptiveText_NotFlagged(string text) => Assert.Empty(_rule.Evaluate(RulesFile.Parse(text)));

    [Fact]
    public void CleanFixture_ReportsNothing()
    {
        Assert.Empty(_rule.Evaluate(Fixtures.Load("clean.md")));
    }
}
