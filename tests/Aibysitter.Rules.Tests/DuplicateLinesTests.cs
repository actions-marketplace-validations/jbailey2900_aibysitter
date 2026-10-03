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
    public void Headings_NotCounted()
    {
        var text = "# Build\n## How to run it locally\n## Other\n## How to run it locally\n";

        Assert.Empty(_rule.Evaluate(RulesFile.Parse(text)));
    }

    [Fact]
    public void InstructionRepeats_OnlyIsolatedInstructionRepeatFlagged()
    {
        var finding = Assert.Single(_rule.Evaluate(Fixtures.Load("v3/R005-instruction-repeats.md")));

        Assert.Equal((39, "Duplicate of line 3."), (finding.Line, finding.Message));
    }

    [Fact]
    public void WrappedContinuation_NotCounted()
    {
        var text = "Keep the summary short and do not\nrestate the rules in this file here.\n\nList the files you changed and do not\nrestate the rules in this file here.\n";

        Assert.Empty(_rule.Evaluate(RulesFile.Parse(text)));
    }

    [Fact]
    public void ParallelBlocks_SameShapeNeighbours_NotFlagged()
    {
        var text = "## Text\n- Call `streamText(a)`.\n- Include basic validation and try/catch.\n\n## Object\n- Call `generateObject(b)`.\n- Include basic validation and try/catch.\n";

        Assert.Empty(_rule.Evaluate(RulesFile.Parse(text)));
    }

    [Fact]
    public void ParallelSections_NotFlagged_RealDuplicateAtOtherPosition_Flagged()
    {
        var finding = Assert.Single(_rule.Evaluate(Fixtures.Load("v3/R005-parallel-sections.md")));

        Assert.Equal((33, "Duplicate of line 29."), (finding.Line, finding.Message));
    }

    [Theory]
    [InlineData("## A\n- one\n- Run the formatter before you commit.\n## B\n- two\n- Run the formatter before you commit.\n", 3, "h2", 2)]
    [InlineData("**Build fails:**\n- Check TypeScript errors now.\n- Verify the paths configuration file.\n", 3, "b", 2)]
    [InlineData("**Build fails:**\n- Check TypeScript errors now.\n\n- Verify the paths configuration file.\n", 4, null, 0)]
    [InlineData("1. **Node Version Check**\n   Run the version command first.\n   Install it when the version is old.\n", 3, "l0", 2)]
    public void SectionPosition_Openers(string text, int lineNumber, string? kind, int position)
    {
        var lines = RulesFile.Parse(text).Lines;
        var result = DuplicateLines.SectionPosition(lines, lineNumber - 1);

        Assert.Equal(kind is null ? null : (kind, position), result);
    }
}
